using DotRush.Server.Extensions;
using LanguageServer.Parameters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Rename;

namespace DotRush.Server.Services;

public class RefactoringService {
    public static RefactoringService Instance { get; private set; } = null!;

    private RefactoringService() {}

    public static void Initialize() {
        var service = new RefactoringService();
        Instance = service;
    }

    public WorkspaceEdit GetWorkspaceEdit(Document document, Position position, string newName) {
        var textDocumentEdits = new List<TextDocumentEdit>();
        var symbol = SemanticConverter.GetSymbolForPosition(position, document.FilePath!);
        if (symbol == null) 
            return new WorkspaceEdit();

        var renameOptions = new SymbolRenameOptions();
        var updatedSolution = Renamer.RenameSymbolAsync(document.Project.Solution, symbol, renameOptions, newName).Result;

        var changes = updatedSolution.GetChanges(document.Project.Solution);
        foreach (var change in changes.GetProjectChanges()) {
            foreach (var documentId in change.GetChangedDocuments()) {
                var newDocument = change.NewProject.GetDocument(documentId);
                var oldDocument = change.OldProject.GetDocument(documentId);
                var textChanges = newDocument!.GetTextChangesAsync(oldDocument!).Result;
                var edits = textChanges.Select(x => x.ToTextEdit(oldDocument!)).ToArray();
                textDocumentEdits.Add(new TextDocumentEdit() { 
                    edits = edits,
                    textDocument = new VersionedTextDocumentIdentifier() { 
                        uri = new Uri(newDocument.FilePath!) 
                    }
                });
            }
        }

        return new WorkspaceEdit() { documentChanges = textDocumentEdits.ToArray() };
    }
}