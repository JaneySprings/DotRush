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
    private readonly WorkspaceService solutionService;

    public ImplementationHandler(WorkspaceService solutionService) {
        this.solutionService = solutionService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.ImplementationProvider = true;
    }
    protected override async Task<ImplementationResponse?> Handle(ImplementationParams request, CancellationToken cancellationToken) {
        var documentIds = solutionService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath);
        if (documentIds == null)
            return null;

        var result = new NullableValueCollection<ProtocolModels.Location>();
        foreach (var documentId in documentIds) {
            var document = solutionService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken).ConfigureAwait(false);
            if (symbol == null || solutionService.Solution == null)
                continue;

            var symbols = await SymbolFinder.FindImplementationsAsync(symbol, solutionService.Solution, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (symbols != null)
                result.AddRange(symbols.SelectMany(it => it.Locations).Select(it => it.ToLocation()));

            symbols = await SymbolFinder.FindOverridesAsync(symbol, solutionService.Solution, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (symbols != null)
                result.AddRange(symbols.SelectMany(it => it.Locations).Select(it => it.ToLocation()));

            if (symbol is INamedTypeSymbol namedTypeSymbol) {
                symbols = await SymbolFinder.FindDerivedClassesAsync(namedTypeSymbol, solutionService.Solution, transitive: false, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (symbols != null)
                    result.AddRange(symbols.SelectMany(it => it.Locations).Select(it => it.ToLocation()));

                symbols = await SymbolFinder.FindDerivedInterfacesAsync(namedTypeSymbol, solutionService.Solution, transitive: false, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (symbols != null)
                    result.AddRange(symbols.SelectMany(it => it.Locations).Select(it => it.ToLocation()));
            }
        }

        return new ImplementationResponse(result.ToNonNullableList());
    }
}