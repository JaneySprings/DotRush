using dotRush.Server.Extensions;
using LanguageServer.Parameters;
using LanguageServer.Parameters.TextDocument;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;

namespace dotRush.Server.Services;

public class RefactoringService {
    public static RefactoringService Instance { get; private set; } = null!;

    private RefactoringService() {}

    public static void Initialize() {
        var service = new RefactoringService();
        Instance = service;
    }

    public WorkspaceEdit GetWorkspaceEdit(Document document, Position position, string newName) {
        var workspaceEdit = new WorkspaceEdit() { changes = new Dictionary<Uri, TextEdit[]>() };
        var symbol = SemanticConverter.GetSymbolForPosition(position, document.FilePath!);
        if (symbol == null) 
            return workspaceEdit;

        var renameOptions = new SymbolRenameOptions();
        var updatedSolution = Renamer.RenameSymbolAsync(document.Project.Solution, symbol, renameOptions, newName).Result;

        var changes = updatedSolution.GetChanges(document.Project.Solution);
        foreach (var change in changes.GetProjectChanges()) {
            foreach (var documentId in change.GetChangedDocuments()) {
                var newDocument = change.NewProject.GetDocument(documentId);
                var oldDocument = change.OldProject.GetDocument(documentId);
                var textChanges = newDocument!.GetTextChangesAsync(oldDocument!).Result;
                var edits = textChanges.Select(x => x.ToTextEdit(oldDocument!)).ToArray();
                workspaceEdit.changes.Add(new Uri(newDocument.FilePath!), edits);
            }
        }

        return workspaceEdit;
    }
}