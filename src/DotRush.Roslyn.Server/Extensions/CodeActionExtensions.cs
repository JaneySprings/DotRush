using Microsoft.CodeAnalysis.CodeActions;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Message.CodeAction;
using System.Reflection;
using Microsoft.CodeAnalysis;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using DotRush.Common.Extensions;

namespace DotRush.Roslyn.Server.Extensions;

public static class CodeActionExtensions {
    private static FieldInfo? inNewFileField;

    public static ProtocolModels.CodeAction ToCodeAction(this CodeAction codeAction, ProtocolModels.CodeActionKind kind) {
        return new ProtocolModels.CodeAction() {
            IsPreferred = codeAction.Priority == CodeActionPriority.High,
            Kind = kind,
            Title = codeAction.Title,
            Data = codeAction.GetUniqueId(),
        };
    }
    private static ProtocolModels.CodeAction ToCodeAction(this CodeAction codeAction, ProtocolModels.CodeActionKind kind, string title) {
        return new ProtocolModels.CodeAction() {
            IsPreferred = codeAction.Priority == CodeActionPriority.High,
            Kind = kind,
            Title = title,
            Data = codeAction.GetUniqueId(),
        };
    }

    public static IEnumerable<Tuple<CodeAction, ProtocolModels.CodeAction>> ToFlattenCodeActions(this CodeAction codeAction, ProtocolModels.CodeActionKind kind) {
        if (codeAction.NestedActions.IsEmpty)
            return new[] { new Tuple<CodeAction, ProtocolModels.CodeAction>(codeAction, codeAction.ToCodeAction(kind)) };

        return codeAction.NestedActions.SelectMany(it => it.ToFlattenCodeActionsCore(kind, codeAction.Title));
    }
    private static IEnumerable<Tuple<CodeAction, ProtocolModels.CodeAction>> ToFlattenCodeActionsCore(this CodeAction codeAction, ProtocolModels.CodeActionKind kind, string parentTitle) {
        if (codeAction.NestedActions.IsEmpty)
            return new[] { new Tuple<CodeAction, ProtocolModels.CodeAction>(codeAction, codeAction.ToCodeAction(kind, codeAction.GetSubject(parentTitle))) };

        return codeAction.NestedActions.SelectMany(it => it.ToFlattenCodeActionsCore(kind, codeAction.GetSubject(parentTitle)));
    }
    private static string GetSubject(this CodeAction codeAction, string parentTitle) {
        if (string.IsNullOrEmpty(parentTitle))
            return codeAction.Title;
        if (codeAction.Title.StartsWithUpper())
            return codeAction.Title;

        return $"{parentTitle} {codeAction.Title}";
    }

    public static async Task<ProtocolModels.CodeAction?> ResolveCodeActionAsync(this CodeAction codeAction, Solution solution, CancellationToken cancellationToken) {
        var textDocumentEdits = new Dictionary<DocumentUri, List<TextEdit>>();
        var operations = await codeAction.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var operation in operations) {
            if (operation is ApplyChangesOperation applyChangesOperation) {
                var solutionChanges = applyChangesOperation.ChangedSolution.GetChanges(solution);
                foreach (var projectChanges in solutionChanges.GetProjectChanges()) {
                    foreach (var documentChanges in projectChanges.GetChangedDocuments()) {
                        var newDocument = projectChanges.NewProject.GetDocument(documentChanges);
                        var oldDocument = solution.GetDocument(newDocument?.Id);
                        if (oldDocument?.FilePath == null || newDocument?.FilePath == null)
                            continue;

                        var sourceText = await oldDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                        var textEdits = new List<TextEdit>();
                        var textChanges = await newDocument.GetTextChangesAsync(oldDocument, cancellationToken).ConfigureAwait(false);
                        textEdits.AddRange(textChanges.Select(x => new TextEdit() {
                            NewText = x.NewText ?? string.Empty,
                            Range = x.Span.ToRange(sourceText),
                        }));

                        if (textEdits.Count == 0)
                            continue;

                        textDocumentEdits.Add(newDocument.FilePath, textEdits);
                    }
                }
            }
        }

        return new ProtocolModels.CodeAction() {
            Kind = ProtocolModels.CodeActionKind.QuickFix,
            Title = codeAction.Title,
            Edit = new WorkspaceEdit() {
                Changes = textDocumentEdits
            }
        };
    }

    public static bool IsBlacklisted(this CodeAction codeAction) {
        var actionType = codeAction.GetType();
        var actionName = actionType.Name;
        if (actionName == "GenerateTypeCodeActionWithOption" || actionName == "ChangeSignatureCodeAction" || actionName == "PullMemberUpWithDialogCodeAction")
            return true;

        if (actionName != "GenerateTypeCodeAction")
            return false;

        if (inNewFileField == null)
            inNewFileField = actionType.GetField("_inNewFile", BindingFlags.Instance | BindingFlags.NonPublic);

        var isNewFile = inNewFileField?.GetValue(codeAction);
        return isNewFile != null && (bool)isNewFile;
    }
}