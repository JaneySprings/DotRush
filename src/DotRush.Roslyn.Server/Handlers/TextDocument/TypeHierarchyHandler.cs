using DotRush.Roslyn.CodeAnalysis;
using DotRush.Common.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.TypeHierarchy;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextDocument;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class TypeHierarchyHandler : TypeHierarchyHandlerBase {
    private readonly NavigationService navigationService;
    private readonly Dictionary<int, ISymbol> typeHierarchyCache;

    public TypeHierarchyHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
        this.typeHierarchyCache = new Dictionary<int, ISymbol>();
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.TypeHierarchyProvider = true;
    }
    protected override Task<TypeHierarchyResponse?> Handle(TypeHierarchyPrepareParams typeHierarchyPrepareParams, CancellationToken cancellationToken) {
        typeHierarchyCache.Clear();

        return SafeExtensions.InvokeAsync(async () => {
            var documentId = navigationService.Solution?.GetDocumentIdsWithFilePathV2(typeHierarchyPrepareParams.TextDocument.Uri.FileSystemPath).FirstOrDefault();
            if (documentId == null || navigationService.Solution == null)
                return null;

            var result = new List<TypeHierarchyItem>();
            var document = navigationService.Solution.GetDocument(documentId);
            if (document == null)
                return null;

            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, typeHierarchyPrepareParams.Position.ToOffset(sourceText), cancellationToken).ConfigureAwait(false);
            var typeSymbol = symbol.GetTypeSymbol();
            if (typeSymbol == null)
                return null;

            result.Add(CreateTypeHierarchyItem(typeSymbol, typeHierarchyPrepareParams));
            return new TypeHierarchyResponse(result);
        });
    }
    protected override Task<TypeHierarchyResponse?> Handle(TypeHierarchySupertypesParams typeHierarchySupertypesParams, CancellationToken cancellationToken) {
        if (typeHierarchySupertypesParams.Item.Data?.Value == null)
            return Task.FromResult<TypeHierarchyResponse?>(null);

        var result = new List<TypeHierarchyItem>();
        var symbol = typeHierarchyCache.GetValueOrDefault((int)typeHierarchySupertypesParams.Item.Data.Value);
        if (symbol == null || symbol is not ITypeSymbol typeSymbol)
            return Task.FromResult<TypeHierarchyResponse?>(null);

        if (typeSymbol.BaseType != null)
            result.Add(CreateTypeHierarchyItem(typeSymbol.BaseType, typeHierarchySupertypesParams.Item));
        foreach (var iface in typeSymbol.Interfaces)
            result.Add(CreateTypeHierarchyItem(iface, typeHierarchySupertypesParams.Item));

        return Task.FromResult<TypeHierarchyResponse?>(new TypeHierarchyResponse(result));
    }
    protected override Task<TypeHierarchyResponse?> Handle(TypeHierarchySubtypesParams typeHierarchySubtypesParams, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync(async () => {
            if (typeHierarchySubtypesParams.Item.Data?.Value == null || navigationService.Solution == null)
                return null;

            var result = new List<TypeHierarchyItem>();
            var symbol = typeHierarchyCache.GetValueOrDefault((int)typeHierarchySubtypesParams.Item.Data.Value);
            if (symbol == null || symbol is not INamedTypeSymbol namedTypeSymbol)
                return null;

            var subtypes = await SymbolFinder.FindDerivedInterfacesAsync(namedTypeSymbol, navigationService.Solution, transitive: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            foreach (var subtype in subtypes)
                result.Add(CreateTypeHierarchyItem(subtype, typeHierarchySubtypesParams.Item));

            subtypes = await SymbolFinder.FindDerivedClassesAsync(namedTypeSymbol, navigationService.Solution, transitive: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            foreach (var subtype in subtypes)
                result.Add(CreateTypeHierarchyItem(subtype, typeHierarchySubtypesParams.Item));

            subtypes = await SymbolFinder.FindImplementationsAsync(namedTypeSymbol, navigationService.Solution, transitive: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            foreach (var subtype in subtypes)
                result.Add(CreateTypeHierarchyItem(subtype, typeHierarchySubtypesParams.Item));

            return new TypeHierarchyResponse(result);
        });
    }

    private TypeHierarchyItem CreateTypeHierarchyItem(ISymbol symbol, TextDocumentPositionParams fallbackParams) {
        return CreateTypeHierarchyItem(symbol, fallbackParams.TextDocument.Uri.FileSystemPath, fallbackParams.Position.ToRange());
    }
    private TypeHierarchyItem CreateTypeHierarchyItem(ISymbol symbol, TypeHierarchyItem fallbackItem) {
        return CreateTypeHierarchyItem(symbol, fallbackItem.Uri.FileSystemPath, PositionExtensions.EmptyRange);
    }
    private TypeHierarchyItem CreateTypeHierarchyItem(ISymbol symbol, string fallbackUri, DocumentRange fallbackRange) {
        var key = symbol.ToDisplayString().GetHashCode();
        typeHierarchyCache.TryAdd(key, symbol);
        
        var location = symbol.Locations.FirstOrDefault();
        return new TypeHierarchyItem {
            Name = symbol.ToDisplayString(DisplayFormat.Member),
            Kind = symbol.ToSymbolKind(),
            Detail = symbol.ToDisplayString(DisplayFormat.Default),
            Uri = location?.SourceTree?.FilePath ?? fallbackUri,
            Range = location?.ToRange() ?? fallbackRange,
            SelectionRange = location?.ToRange() ?? fallbackRange,
            Data = key
        };
    }
}