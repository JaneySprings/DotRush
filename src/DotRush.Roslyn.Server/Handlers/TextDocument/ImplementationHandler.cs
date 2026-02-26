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

        var result = new NullableValueCollection<ProtocolModels.Location>();
        foreach (var documentId in documentIds) {
            var document = navigationService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken).ConfigureAwait(false);
            if (symbol == null || navigationService.Solution == null)
                continue;

            var symbols = await SymbolFinder.FindImplementationsAsync(symbol, navigationService.Solution, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (symbols != null)
                foreach (var location in symbols.SelectMany(it => it.Locations))
                    await AddResolvedLocationAsync(result, location, document.Project, cancellationToken).ConfigureAwait(false);

            symbols = await SymbolFinder.FindOverridesAsync(symbol, navigationService.Solution, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (symbols != null)
                foreach (var location in symbols.SelectMany(it => it.Locations))
                    await AddResolvedLocationAsync(result, location, document.Project, cancellationToken).ConfigureAwait(false);

            if (symbol is INamedTypeSymbol namedTypeSymbol) {
                symbols = await SymbolFinder.FindDerivedClassesAsync(namedTypeSymbol, navigationService.Solution, transitive: false, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (symbols != null)
                    foreach (var location in symbols.SelectMany(it => it.Locations))
                        await AddResolvedLocationAsync(result, location, document.Project, cancellationToken).ConfigureAwait(false);

                symbols = await SymbolFinder.FindDerivedInterfacesAsync(namedTypeSymbol, navigationService.Solution, transitive: false, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (symbols != null)
                    foreach (var location in symbols.SelectMany(it => it.Locations))
                        await AddResolvedLocationAsync(result, location, document.Project, cancellationToken).ConfigureAwait(false);
            }
        }

        return new ImplementationResponse(result.ToNonNullableList());
    }

    async Task AddResolvedLocationAsync(NullableValueCollection<ProtocolModels.Location> result, Location location, Project project, CancellationToken cancellationToken) {
        if (!location.IsInSource || location.SourceTree == null)
            return;

        var filePath = location.SourceTree?.FilePath ?? string.Empty;
        if (!File.Exists(filePath))
            filePath = await navigationService.EmitCompilerGeneratedFileAsync(location, project, cancellationToken).ConfigureAwait(false);

        result.Add(location.ToLocation(filePath));
    }
}