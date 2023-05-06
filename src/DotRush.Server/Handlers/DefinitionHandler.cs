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
        var document = this.solutionService.GetDocumentByPath(request.TextDocument.Uri.GetFileSystemPath());
        if (document == null)
            return new LocationOrLocationLinks();
    
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(document), cancellationToken);
        if (symbol == null || this.solutionService.Solution == null) 
            return new LocationOrLocationLinks();
        
        var definition = await SymbolFinder.FindSourceDefinitionAsync(symbol, this.solutionService.Solution, cancellationToken);
        if (definition == null) 
            return new LocationOrLocationLinks();

        return new LocationOrLocationLinks(definition.Locations.Select(loc => new LocationOrLocationLink(loc.ToLocation(this.solutionService)!)));
    }
}