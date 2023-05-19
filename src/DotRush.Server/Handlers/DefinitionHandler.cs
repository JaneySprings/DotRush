using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Handlers;

public class DefinitionHandler : DefinitionHandlerBase {
    private SolutionService solutionService;

    public DefinitionHandler(SolutionService solutionService) {
        this.solutionService = solutionService;
    }

    protected override DefinitionRegistrationOptions CreateRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities) {
        return new DefinitionRegistrationOptions();
    }

    public override async Task<LocationOrLocationLinks> Handle(DefinitionParams request, CancellationToken cancellationToken) {
        var documentIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
        if (documentIds == null)
            return new LocationOrLocationLinks();

        var result = new List<LocationOrLocationLink>();
        foreach (var documentId in documentIds) {
            var document = this.solutionService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
            if (symbol == null || this.solutionService.Solution == null) 
                continue;
            
            var definition = await SymbolFinder.FindSourceDefinitionAsync(symbol, this.solutionService.Solution, cancellationToken);
            if (definition == null) 
                continue;

            result.AddRange(definition.Locations.Select(loc => new LocationOrLocationLink(loc.ToLocation()!)));
        }

        return new LocationOrLocationLinks(result);
    }
}