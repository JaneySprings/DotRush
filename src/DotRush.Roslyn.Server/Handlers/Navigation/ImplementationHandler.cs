using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers;

public class ImplementationHandler : ImplementationHandlerBase {
    private WorkspaceService solutionService;

    public ImplementationHandler(WorkspaceService solutionService) {
        this.solutionService = solutionService;
    }

    protected override ImplementationRegistrationOptions CreateRegistrationOptions(ImplementationCapability capability, ClientCapabilities clientCapabilities) {
        return new ImplementationRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }

    public override async Task<LocationOrLocationLinks?> Handle(ImplementationParams request, CancellationToken cancellationToken) {
        var documentIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
        if (documentIds == null)
            return null;

        var resultSymbols = new List<ISymbol>();
        foreach (var documentId in documentIds) {
            var document = this.solutionService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
            if (symbol == null || this.solutionService.Solution == null) 
                continue;

            var symbols = await SymbolFinder.FindImplementationsAsync(symbol, this.solutionService.Solution, cancellationToken: cancellationToken);
            if (symbols != null)
                resultSymbols.AddRange(symbols);

            if (symbol is IMethodSymbol methodSymbol) {
                symbols = await SymbolFinder.FindOverridesAsync(methodSymbol, this.solutionService.Solution, cancellationToken: cancellationToken);
                if (symbols != null)
                    resultSymbols.AddRange(symbols);
            }
            if (symbol is INamedTypeSymbol namedTypeSymbol) {
                symbols = await SymbolFinder.FindDerivedClassesAsync(namedTypeSymbol, this.solutionService.Solution, cancellationToken: cancellationToken);
                if (symbols != null)
                    resultSymbols.AddRange(symbols);
                
                symbols = await SymbolFinder.FindDerivedInterfacesAsync(namedTypeSymbol, this.solutionService.Solution, cancellationToken: cancellationToken);
                if (symbols != null)
                    resultSymbols.AddRange(symbols);
            }
        }

        return new LocationCollection()
            .AddRange(resultSymbols
            .SelectMany(i => i.Locations)
            .Select(loc => loc.ToLocation()))
            .ToLocationOrLocationLinks();
    }
}