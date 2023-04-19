using DotRush.Server.Extensions;
using LanguageServer.Parameters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;

namespace DotRush.Server.Services;

public class RefactoringService {
    public static WorkspaceEdit GetWorkspaceEdit(string filePath, Position position, string newName) {
        var textDocumentEdits = new List<TextDocumentEdit>();
        var document = DocumentService.GetDocumentByPath(filePath);
        if (document == null)
            return new WorkspaceEdit();
        
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
                        uri = newDocument.FilePath?.ToUri()
                    }
                });
            }
        }

        return new WorkspaceEdit() { documentChanges = textDocumentEdits.ToArray() };
    }

    public static List<TextEdit> GetFormattingEdits(string filePath) {
        var edits = new List<TextEdit>();
        var document = DocumentService.GetDocumentByPath(filePath);
        if (document == null) 
            return edits;

        var formattedDoc = Formatter.FormatAsync(document).Result;
        var textChanges = formattedDoc.GetTextChangesAsync(document).Result;
        return textChanges.Select(x => x.ToTextEdit(document)).ToList();
    }

    public static List<TextEdit> GetFormattingEdits(string filePath, LanguageServer.Parameters.Range range) {
        var edits = new List<TextEdit>();
        var document = DocumentService.GetDocumentByPath(filePath);
        if (document == null) 
            return edits;

        var formattedDoc = Formatter.FormatAsync(document, range.ToTextSpan(document)).Result;
        var textChanges = formattedDoc.GetTextChangesAsync(document).Result;
        return textChanges.Select(x => x.ToTextEdit(document)).ToList();
    }
}