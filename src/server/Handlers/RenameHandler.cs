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
        var documentEdits = new Dictionary<DocumentUri, IEnumerable<TextEdit>>();
        var documentId = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath()).FirstOrDefault();
        var document = this.solutionService.Solution?.GetDocument(documentId);
        if (document == null || this.solutionService.Solution == null)
            return null;
        
        var sourceText = await document.GetTextAsync(cancellationToken);
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
        if (symbol == null) 
            return new WorkspaceEdit();

        var renameOptions = new SymbolRenameOptions();
        var updatedSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, symbol, renameOptions, request.NewName, cancellationToken);
        var changes = updatedSolution.GetChanges(document.Project.Solution);

        foreach (var change in changes.GetProjectChanges()) {
            if (change.NewProject.FilePath == null || change.OldProject.FilePath == null)
                continue;

            foreach (var changedDocId in change.GetChangedDocuments()) {
                var newDocument = change.NewProject.GetDocument(changedDocId);
                var oldDocument = change.OldProject.GetDocument(changedDocId);
                
                if (newDocument?.FilePath == null || oldDocument?.FilePath == null)
                    continue;

                var oldSourceText = await oldDocument.GetTextAsync(cancellationToken);
                var textChanges = await newDocument.GetTextChangesAsync(oldDocument, cancellationToken);
                var edits = textChanges.Select(x => x.ToTextEdit(oldSourceText));
 
                if (!edits.Any())
                    continue;

                documentEdits.TryAdd(DocumentUri.From(newDocument.FilePath), edits);
            }
        }

        return new WorkspaceEdit() { Changes = documentEdits };
    }
}