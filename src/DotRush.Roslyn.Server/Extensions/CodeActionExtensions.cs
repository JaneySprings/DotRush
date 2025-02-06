using Microsoft.CodeAnalysis.CodeActions;
using DotRush.Roslyn.Server.Services;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Message.CodeAction;
using System.Reflection;
using Microsoft.CodeAnalysis;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextDocument;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Union;

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

    public static async Task<ProtocolModels.CodeAction?> ResolveCodeActionAsync(this CodeAction codeAction, WorkspaceService solutionService, CancellationToken cancellationToken) {
        if (solutionService.Solution == null)
            return null;

        var textDocumentEdits = new List<TextDocumentEdit>();
        var operations = await codeAction.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var operation in operations) {
            if (operation is ApplyChangesOperation applyChangesOperation) {
                var solutionChanges = applyChangesOperation.ChangedSolution.GetChanges(solutionService.Solution);
                foreach (var projectChanges in solutionChanges.GetProjectChanges()) {
                    foreach (var documentChanges in projectChanges.GetChangedDocuments()) {
                        var newDocument = projectChanges.NewProject.GetDocument(documentChanges);
                        var oldDocument = solutionService.Solution.GetDocument(newDocument?.Id);
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

                        textDocumentEdits.Add(new TextDocumentEdit() {
                            Edits = textEdits,
                            TextDocument = new OptionalVersionedTextDocumentIdentifier(newDocument.FilePath, null)
                        });
                    }
                }
            }
        }

        return new ProtocolModels.CodeAction() {
            Kind = ProtocolModels.CodeActionKind.QuickFix,
            Title = codeAction.Title,
            Edit = new WorkspaceEdit() {
                DocumentChanges = new WorkspaceEditDocumentChanges(textDocumentEdits.ToList()),
            },
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