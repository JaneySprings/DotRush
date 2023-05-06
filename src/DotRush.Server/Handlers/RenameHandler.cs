using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Handlers;

public class RenameHandler : RenameHandlerBase {
    private SolutionService solutionService;

    public RenameHandler(SolutionService solutionService) {
        this.solutionService = solutionService;
    }

    protected override RenameRegistrationOptions CreateRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities) {
        return new RenameRegistrationOptions();
    }

    public override async Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken cancellationToken) {
        var textDocumentEdits = new List<WorkspaceEditDocumentChange>();
        var document = this.solutionService.GetDocumentByPath(request.TextDocument.Uri.GetFileSystemPath());
        if (document == null)
            return null;
        
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(document), cancellationToken);
        if (symbol == null) 
            return new WorkspaceEdit();

        var renameOptions = new SymbolRenameOptions();
        var updatedSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, symbol, renameOptions, request.NewName, cancellationToken);
        var changes = updatedSolution.GetChanges(document.Project.Solution);

        foreach (var change in changes.GetProjectChanges()) {
            foreach (var documentId in change.GetChangedDocuments()) {
                var newDocument = change.NewProject.GetDocument(documentId);
                var oldDocument = change.OldProject.GetDocument(documentId);
                var textChanges = newDocument!.GetTextChangesAsync(oldDocument!).Result;
                var edits = textChanges.Select(x => x.ToTextEdit(oldDocument!)).ToArray();
                textDocumentEdits.Add(new TextDocumentEdit() { 
                    Edits = edits,
                    TextDocument = new OptionalVersionedTextDocumentIdentifier() { 
                        Uri = DocumentUri.From(newDocument.FilePath!)
                    }
                });
            }
        }

        return new WorkspaceEdit() { 
            DocumentChanges = textDocumentEdits
        };
    }
}