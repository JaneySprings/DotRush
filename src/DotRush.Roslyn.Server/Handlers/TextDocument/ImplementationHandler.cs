using DotRush.Common.Extensions;
using DotRush.Protocol.Handlers;
using DotRush.Protocol.Models;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Location = Microsoft.CodeAnalysis.Location;
using ProtocolModels = DotRush.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class ImplementationHandler : ImplementationHandlerBase {
    private readonly NavigationService navigationService;

    public ImplementationHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities) {
        serverCapabilities.ImplementationProvider = true;
    }
    protected override Task<List<ProtocolModels.Location>?> Handle(ImplementationParams request, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync<List<ProtocolModels.Location>?>(async () => {
            var documentPath = request.TextDocument.Uri.FileSystemPath;
            var solution = navigationService.GetRequiredSolution(documentPath);
            var documentIds = solution?.GetDocumentIdsWithFilePathV2(documentPath);
            if (documentIds == null)
                return null;

            var result = new HashSet<ProtocolModels.Location>();
            foreach (var documentId in documentIds) {
                var implementationLocations = new List<Location>();
                var document = solution?.GetDocument(documentId);
                if (document == null)
                    continue;

                var sourceText = await document.GetTextAsync(cancellationToken);
                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
                if (symbol == null || solution == null)
                    continue;

                var symbols = await SymbolFinder.FindImplementationsAsync(symbol, solution, cancellationToken: cancellationToken);
                if (symbols != null)
                    implementationLocations.AddRange(symbols.SelectMany(it => it.Locations));

                symbols = await SymbolFinder.FindOverridesAsync(symbol, solution, cancellationToken: cancellationToken);
                if (symbols != null)
                    implementationLocations.AddRange(symbols.SelectMany(it => it.Locations));

                if (symbol is INamedTypeSymbol namedTypeSymbol) {
                    symbols = await SymbolFinder.FindDerivedClassesAsync(namedTypeSymbol, solution, transitive: false, cancellationToken: cancellationToken);
                    if (symbols != null)
                        implementationLocations.AddRange(symbols.SelectMany(it => it.Locations));

                    symbols = await SymbolFinder.FindDerivedInterfacesAsync(namedTypeSymbol, solution, transitive: false, cancellationToken: cancellationToken);
                    if (symbols != null)
                        implementationLocations.AddRange(symbols.SelectMany(it => it.Locations));
                }
                foreach (Location location in implementationLocations) {
                    if (!location.IsInSource || location.SourceTree == null)
                        continue;

                    if (!File.Exists(location.SourceTree.FilePath)) {
                        var generatedSpan = await navigationService.EmitCompilerGeneratedFileAsync(location, document.Project, cancellationToken);
                        if (generatedSpan != null)
                            result.Add(generatedSpan.Value.ToLocation());
                        continue;
                    }

                    result.Add(location.ToLocation());
                }
            }
            return result.ToList();
        });
    }
}
