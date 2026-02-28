using DotRush.Common.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Implementation;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Model;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class ImplementationHandler : ImplementationHandlerBase {
    private readonly NavigationService navigationService;

    public ImplementationHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.ImplementationProvider = true;
    }
    protected override async Task<ImplementationResponse?> Handle(ImplementationParams request, CancellationToken cancellationToken) {
        var documentIds = navigationService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath);
        if (documentIds == null)
            return null;

        var result = new HashSet<ProtocolModels.Location>();
        foreach (var documentId in documentIds) {
            var implementationLocations = new NullableCollection<Location>();
            var document = navigationService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken).ConfigureAwait(false);
            if (symbol == null || navigationService.Solution == null)
                continue;

            var symbols = await SymbolFinder.FindImplementationsAsync(symbol, navigationService.Solution, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (symbols != null)
                implementationLocations.AddRange(symbols.SelectMany(it => it.Locations));

            symbols = await SymbolFinder.FindOverridesAsync(symbol, navigationService.Solution, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (symbols != null)
                implementationLocations.AddRange(symbols.SelectMany(it => it.Locations));

            if (symbol is INamedTypeSymbol namedTypeSymbol) {
                symbols = await SymbolFinder.FindDerivedClassesAsync(namedTypeSymbol, navigationService.Solution, transitive: false, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (symbols != null)
                    implementationLocations.AddRange(symbols.SelectMany(it => it.Locations));

                symbols = await SymbolFinder.FindDerivedInterfacesAsync(namedTypeSymbol, navigationService.Solution, transitive: false, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (symbols != null)
                    implementationLocations.AddRange(symbols.SelectMany(it => it.Locations));
            }
            foreach (Location? location in implementationLocations) {
                if (location == null)
                    continue;
                var filePath = location.SourceTree?.FilePath ?? string.Empty;
                if (!File.Exists(filePath))
                    filePath = await navigationService.EmitCompilerGeneratedFileAsync(location, document.Project, cancellationToken).ConfigureAwait(false);

                var serverLocation = location.ToLocation(filePath);
                if (serverLocation != null)
                    result.Add(serverLocation.Value);
            }
        }
        return new ImplementationResponse(result.ToList());
    }
}