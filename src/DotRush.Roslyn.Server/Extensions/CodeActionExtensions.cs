using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.File;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextDocument;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Message.CodeAction;

namespace DotRush.Roslyn.Server.Extensions;

public static class CodeActionExtensions {
    public static ProtocolModels.CodeAction ToCodeAction(this CodeAction codeAction, ProtocolModels.CodeActionKind kind, string? title = null) {
        return new ProtocolModels.CodeAction() {
            IsPreferred = codeAction.Priority == CodeActionPriority.High,
            Kind = kind,
            Title = title ?? codeAction.Title,
            Data = codeAction.GetUniqueId(),
        };
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

    public static void ToFlattenCodeActions(this CodeAction codeAction, Action<CodeAction, string> handler) {
        ToFlattenCore(codeAction, handler, null);

        static void ToFlattenCore(CodeAction codeAction, Action<CodeAction, string> handler, string? parentTitle) {
            if (codeAction.IsBlacklisted())
                return;
            if (codeAction.NestedActions.IsEmpty) {
                handler.Invoke(codeAction, codeAction.Title);
                return;
            }
            foreach (var nestedAction in codeAction.NestedActions)
                ToFlattenCore(nestedAction, handler, GetSubject(parentTitle, codeAction.Title));
        }
        static string GetSubject(string? parentTitle, string currentTitle) {
            if (string.IsNullOrEmpty(parentTitle))
                return currentTitle;
            return currentTitle.StartsWithUpper() ? currentTitle : $"{parentTitle} {currentTitle}";
        }
    }

    public static async Task<List<IDocumentChange>> ToDocumentChangesAsync(this SolutionChanges solutionChanges, CancellationToken cancellationToken) {
        var documentChanges = new List<IDocumentChange>();

        foreach (var projectChanges in solutionChanges.GetProjectChanges()) {
            // Update
            foreach (var changedDocumentId in projectChanges.GetChangedDocuments()) {
                var newDocument = projectChanges.NewProject.GetDocument(changedDocumentId);
                var oldDocument = projectChanges.OldProject.GetDocument(changedDocumentId);
                if (oldDocument?.FilePath == null || newDocument?.FilePath == null)
                    continue;

                if (newDocument.Name != oldDocument.Name) {
                    var newDocumentPath = Path.Combine(Path.GetDirectoryName(oldDocument.FilePath)!, newDocument.Name);
                    documentChanges.Add(new RenameFile(oldDocument.FilePath, newDocumentPath, null, null));
                    continue; // Or changes in new file?
                }

                var sourceText = await oldDocument.GetTextAsync(cancellationToken);
                var textChanges = await newDocument.GetTextChangesAsync(oldDocument, cancellationToken);
                documentChanges.Add(new TextDocumentEdit() {
                    TextDocument = new OptionalVersionedTextDocumentIdentifier(oldDocument.FilePath, null),
                    Edits = textChanges.Select(change => new TextEdit() {
                        NewText = change.NewText ?? string.Empty,
                        Range = change.Span.ToRange(sourceText),
                    }).ToList(),
                });
            }
            // Create
            foreach (var addedDocumentId in projectChanges.GetAddedDocuments()) {
                var newDocument = projectChanges.NewProject.GetDocument(addedDocumentId)!;
                var documentFilePath = newDocument.GetDocumentFilePath();
                var sourceText = await newDocument.GetTextAsync(cancellationToken);
                documentChanges.Add(new CreateFile(documentFilePath, new CreateFileOptions(Overwrite: true, IgnoreIfExists: false), null));
                documentChanges.Add(new TextDocumentEdit() {
                    TextDocument = new OptionalVersionedTextDocumentIdentifier(documentFilePath, null),
                    Edits = new List<TextEdit> { new TextEdit() { NewText = sourceText.ToString(), Range = default } },
                });
            }
            // Delete
            foreach (var removedDocumentId in projectChanges.GetRemovedDocuments()) {
                var oldDocument = projectChanges.OldProject.GetDocument(removedDocumentId)!;
                if (oldDocument.FilePath != null)
                    documentChanges.Add(new DeleteFile(oldDocument.FilePath, new DeleteFileOptions(Recursive: false, IgnoreIfNotExists: true), null));
            }
        }

        return documentChanges;
    }
}

public class DocumentChangeEqualityComparer : IEqualityComparer<IDocumentChange> {
    public static DocumentChangeEqualityComparer Default { get; } = new DocumentChangeEqualityComparer();

    public bool Equals(IDocumentChange? x, IDocumentChange? y) {
        if (x == null && y == null)
            return true;
        if (x == null || y == null)
            return false;
        return GetHashCode(x) == GetHashCode(y);
    }
    public int GetHashCode(IDocumentChange obj) {
        switch (obj) {
            case TextDocumentEdit textDocumentEdit:
                return HashCode.Combine(textDocumentEdit.TextDocument.Uri.FileSystemPath, 'U');
            case RenameFile renameEdit:
                return HashCode.Combine(renameEdit.OldUri.FileSystemPath, 'R');
            case CreateFile createEdit:
                return HashCode.Combine(createEdit.Uri.FileSystemPath, 'C');
            case DeleteFile deleteEdit:
                return HashCode.Combine(deleteEdit.Uri.FileSystemPath, 'D');
            default:
                return obj.GetHashCode();
        }
    }
}