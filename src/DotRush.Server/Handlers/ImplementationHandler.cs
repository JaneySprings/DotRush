using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Handlers;

public class ImplementationHandler : ImplementationHandlerBase {
    private SolutionService solutionService;

    public ImplementationHandler(SolutionService solutionService) {
        this.solutionService = solutionService;
    }

    protected override ImplementationRegistrationOptions CreateRegistrationOptions(ImplementationCapability capability, ClientCapabilities clientCapabilities) {
        return new ImplementationRegistrationOptions();
    }

    public override async Task<LocationOrLocationLinks> Handle(ImplementationParams request, CancellationToken cancellationToken) {
        var document = this.solutionService.GetDocumentByPath(request.TextDocument.Uri.GetFileSystemPath());
        if (document == null)
            return new LocationOrLocationLinks();

        var sourceText = await document.GetTextAsync(cancellationToken);
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
        if (symbol == null || this.solutionService.Solution == null) 
            return new LocationOrLocationLinks();

        var results = new List<ISymbol>();

        var symbols = await SymbolFinder.FindImplementationsAsync(symbol, this.solutionService.Solution, cancellationToken: cancellationToken);
        if (symbols != null)
            results.AddRange(symbols);

        if (symbol is IMethodSymbol methodSymbol) {
            symbols = await SymbolFinder.FindOverridesAsync(methodSymbol, this.solutionService.Solution, cancellationToken: cancellationToken);
            if (symbols != null)
                results.AddRange(symbols);
        }
        if (symbol is INamedTypeSymbol namedTypeSymbol) {
            symbols = await SymbolFinder.FindDerivedClassesAsync(namedTypeSymbol, this.solutionService.Solution, cancellationToken: cancellationToken);
            if (symbols != null)
                results.AddRange(symbols);
            
            symbols = await SymbolFinder.FindDerivedInterfacesAsync(namedTypeSymbol, this.solutionService.Solution, cancellationToken: cancellationToken);
            if (symbols != null)
                results.AddRange(symbols);
        }

        return new LocationOrLocationLinks(results
            .SelectMany(i => i.Locations)
            .Where(loc => File.Exists(loc.SourceTree?.FilePath))
            .Select(loc => new LocationOrLocationLink(loc.ToLocation()!)));
    }
}