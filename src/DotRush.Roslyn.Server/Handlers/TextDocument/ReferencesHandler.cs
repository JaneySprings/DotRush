using DotRush.Roslyn.Server.Containers;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class ReferencesHandler : ReferencesHandlerBase {
    private readonly WorkspaceService workspaceService;

    public ReferencesHandler(WorkspaceService solutionService) {
        workspaceService = solutionService;
    }

    protected override ReferenceRegistrationOptions CreateRegistrationOptions(ReferenceCapability capability, ClientCapabilities clientCapabilities) {
        return new ReferenceRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }

    public override async Task<LocationContainer?> Handle(ReferenceParams request, CancellationToken cancellationToken) {
        var documentIds = workspaceService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
        if (documentIds == null || workspaceService.Solution == null)
            return null;

        var result = new LocationsContainer();
        foreach (var documentId in documentIds) {
            var document = workspaceService.Solution.GetDocument(documentId);
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