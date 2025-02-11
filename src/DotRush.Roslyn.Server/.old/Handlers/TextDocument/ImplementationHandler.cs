using DotRush.Roslyn.Server.Containers;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class ImplementationHandler : ImplementationHandlerBase {
    private readonly WorkspaceService solutionService;

    public ImplementationHandler(WorkspaceService solutionService) {
        this.solutionService = solutionService;
    }

    protected override ImplementationRegistrationOptions CreateRegistrationOptions(ImplementationCapability capability, ClientCapabilities clientCapabilities) {
        return new ImplementationRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }

    public override async Task<LocationOrLocationLinks?> Handle(ImplementationParams request, CancellationToken cancellationToken) {
        var documentIds = solutionService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.GetFileSystemPath());
        if (documentIds == null)
            return null;

        var result = new LocationsContainer();
        foreach (var documentId in documentIds) {
            var document = solutionService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
            if (symbol == null || solutionService.Solution == null)
                continue;

            var symbols = await SymbolFinder.FindImplementationsAsync(symbol, solutionService.Solution, cancellationToken: cancellationToken);
            if (symbols != null)
                result.AddRange(symbols.SelectMany(it => it.Locations).Select(it => it.ToLocation()));

            if (symbol is IMethodSymbol methodSymbol) {
                symbols = await SymbolFinder.FindOverridesAsync(methodSymbol, solutionService.Solution, cancellationToken: cancellationToken);
                if (symbols != null)
                    result.AddRange(symbols.SelectMany(it => it.Locations).Select(it => it.ToLocation()));
            }
            if (symbol is INamedTypeSymbol namedTypeSymbol) {
                symbols = await SymbolFinder.FindDerivedClassesAsync(namedTypeSymbol, solutionService.Solution, cancellationToken: cancellationToken);
                if (symbols != null)
                    result.AddRange(symbols.SelectMany(it => it.Locations).Select(it => it.ToLocation()));

                symbols = await SymbolFinder.FindDerivedInterfacesAsync(namedTypeSymbol, solutionService.Solution, cancellationToken: cancellationToken);
                if (symbols != null)
                    result.AddRange(symbols.SelectMany(it => it.Locations).Select(it => it.ToLocation()));
            }
        }

        return result.ToLocationOrLocationLinks();
    }
}