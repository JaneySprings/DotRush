using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Handlers;

public class ReferencesHandler : ReferencesHandlerBase {
    private WorkspaceService workspaceService;

    public ReferencesHandler(WorkspaceService solutionService) {
        this.workspaceService = solutionService;
    }

    protected override ReferenceRegistrationOptions CreateRegistrationOptions(ReferenceCapability capability, ClientCapabilities clientCapabilities) {
        return new ReferenceRegistrationOptions();
    }

    public override async Task<LocationContainer?> Handle(ReferenceParams request, CancellationToken cancellationToken) {
        var documentIds = workspaceService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
        if (documentIds == null || workspaceService.Solution == null)
            return null;

        var result = new LocationCollection();
        foreach (var documentId in documentIds) {
            var document = this.workspaceService.Solution.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
            if (symbol == null || symbol.Locations == null) 
                continue;

            var referenceSymbols = await SymbolFinder.FindReferencesAsync(symbol, workspaceService.Solution, cancellationToken);
            var referenceLocations = referenceSymbols
                .SelectMany(r => r.Locations)
                .Where(l => File.Exists(l.Document.FilePath));

            foreach (var location in referenceLocations) {
                var locationSourceText = await location.Document.GetTextAsync(cancellationToken);
                var referenceLocation = location.ToLocation(locationSourceText);
                if (referenceLocation != null)
                    result.Add(referenceLocation);
            }
        }
    
        return result.ToLocationContainer();
    }
}