using Microsoft.CodeAnalysis.CodeActions;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Message.CodeAction;
using Microsoft.CodeAnalysis;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using DotRush.Common.Extensions;
using FileSystemExtensions = DotRush.Common.Extensions.FileSystemExtensions;
using DotRush.Roslyn.Workspaces;
using DotRush.Roslyn.Workspaces.Extensions;
using DotRush.Common.Logging;

namespace DotRush.Roslyn.Server.Extensions;

public static class CodeActionExtensions {
    private const int MaxSubjectLength = 100;

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

        var subject = $"{parentTitle} {codeAction.Title}";
        if (subject.Length > MaxSubjectLength)
            return string.Concat(subject.AsSpan(0, MaxSubjectLength), "...");

        return subject;
    }
    // Requires a specific service to get operations (Not available in the current workspace)
    public static bool IsBlacklisted(this CodeAction codeAction) {
        var codeActionName = codeAction.GetType().Name;
        return codeActionName == "GenerateTypeCodeActionWithOption"
            || codeActionName == "ChangeSignatureCodeAction"
            || codeActionName == "ExtractInterfaceCodeAction"
            || codeActionName == "GenerateOverridesWithDialogCodeAction"
            || codeActionName == "GenerateConstructorWithDialogCodeAction"
            || codeActionName == "PullMemberUpWithDialogCodeAction";
    }

    public static async Task<ProtocolModels.CodeAction?> ResolveCodeActionAsync(this CodeAction codeAction, SolutionController solutionController, CancellationToken cancellationToken) {
        if (solutionController.Solution == null)
            return null;

        var solution = solutionController.Solution;
        var textDocumentEdits = new Dictionary<DocumentUri, List<TextEdit>>();
        var operations = await codeAction.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var operation in operations) {
            if (operation is ApplyChangesOperation applyChangesOperation) {
                var solutionChanges = applyChangesOperation.ChangedSolution.GetChanges(solution);
                foreach (var projectChanges in solutionChanges.GetProjectChanges()) {
                    // Changes
                    foreach (var documentId in projectChanges.GetChangedDocuments()) {
                        var newDocument = projectChanges.NewProject.GetDocument(documentId);
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

                        if (!textDocumentEdits.TryAdd(newDocument.FilePath, textEdits))
                            CurrentSessionLogger.Error($"({codeAction.Title}): Duplicate changes for {newDocument.FilePath} [{projectChanges.NewProject.Name}]");
                    }
                    // New files
                    foreach (var documentId in projectChanges.GetAddedDocuments()) {
                        var newDocument = projectChanges.NewProject.GetDocument(documentId);
                        if (newDocument == null)
                            continue;

                        var newDocumentFilePath = newDocument.FilePath;
                        if (string.IsNullOrEmpty(newDocumentFilePath) && textDocumentEdits.Count != 0)
                            newDocumentFilePath = Path.Combine(Path.GetDirectoryName(textDocumentEdits.Keys.First().FileSystemPath)!, newDocument.Name);
                        if (string.IsNullOrEmpty(newDocumentFilePath))
                            newDocumentFilePath = Path.Combine(projectChanges.NewProject.GetProjectDirectory(), newDocument.Name);

                        var sourceText = await newDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                        FileSystemExtensions.WriteAllText(newDocumentFilePath, sourceText.ToString());
                        solutionController.CreateDocument(newDocumentFilePath);
                        CurrentSessionLogger.Debug($"File created via CodeAction: {newDocumentFilePath}");
                    }
                    // Removed files
                    foreach (var documentId in projectChanges.GetRemovedDocuments()) {
                        var oldDocument = projectChanges.OldProject.GetDocument(documentId);
                        if (oldDocument?.FilePath == null)
                            continue;

                        solutionController.DeleteDocument(oldDocument.FilePath);
                        FileSystemExtensions.TryDeleteFile(oldDocument.FilePath);
                        CurrentSessionLogger.Debug($"File removed via CodeAction: {oldDocument.FilePath}");
                    }
                }
            }
        }

        return new ProtocolModels.CodeAction() {
            Title = codeAction.Title,
            Edit = new WorkspaceEdit() {
                Changes = textDocumentEdits
            }
        };
    }
}