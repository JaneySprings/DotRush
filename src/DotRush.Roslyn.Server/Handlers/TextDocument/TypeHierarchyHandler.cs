using DotRush.Protocol.Handlers;
using DotRush.Protocol.Models;
using DotRush.Roslyn.CodeAnalysis;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class TypeHierarchyHandler : TypeHierarchyHandlerBase {
    private readonly NavigationService navigationService;
    private readonly Dictionary<int, ISymbol> typeHierarchyCache;

    public TypeHierarchyHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
        this.typeHierarchyCache = new Dictionary<int, ISymbol>();
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities) {
        serverCapabilities.TypeHierarchyProvider = true;
    }
    protected override async Task<List<TypeHierarchyItem>?> Handle(TypeHierarchyPrepareParams typeHierarchyPrepareParams, CancellationToken cancellationToken) {
        typeHierarchyCache.Clear();

        var documentPath = typeHierarchyPrepareParams.TextDocument.Uri.FileSystemPath;
        var solution = navigationService.GetRequiredSolution(documentPath);
        var documentId = solution?.GetDocumentIdsWithFilePathV2(typeHierarchyPrepareParams.TextDocument.Uri.FileSystemPath).FirstOrDefault();
        if (documentId == null || solution == null)
            return null;

        var result = new List<TypeHierarchyItem>();
        var document = solution.GetDocument(documentId);
        if (document == null)
            return null;

        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, typeHierarchyPrepareParams.Position.ToOffset(sourceText), cancellationToken).ConfigureAwait(false);
        var typeSymbol = symbol.GetTypeSymbol();
        if (typeSymbol == null)
            return null;

        result.Add(CreateTypeHierarchyItem(typeSymbol, typeHierarchyPrepareParams));
        return result;
    }
    protected override Task<List<TypeHierarchyItem>?> Handle(TypeHierarchySupertypesParams typeHierarchySupertypesParams, CancellationToken cancellationToken) {
        if (typeHierarchySupertypesParams.Item.Data?.Value == null)
            return Task.FromResult<List<TypeHierarchyItem>?>(null);

        var result = new List<TypeHierarchyItem>();
        var symbol = typeHierarchyCache.GetValueOrDefault((int)typeHierarchySupertypesParams.Item.Data.Value);
        if (symbol == null || symbol is not ITypeSymbol typeSymbol)
            return Task.FromResult<List<TypeHierarchyItem>?>(null);

        if (typeSymbol.BaseType != null)
            result.Add(CreateTypeHierarchyItem(typeSymbol.BaseType, typeHierarchySupertypesParams.Item));
        foreach (var iface in typeSymbol.Interfaces)
            result.Add(CreateTypeHierarchyItem(iface, typeHierarchySupertypesParams.Item));

        return Task.FromResult<List<TypeHierarchyItem>?>(result);
    }
    protected override async Task<List<TypeHierarchyItem>?> Handle(TypeHierarchySubtypesParams typeHierarchySubtypesParams, CancellationToken cancellationToken) {
        if (typeHierarchySubtypesParams.Item.Data?.Value == null || navigationService.HostSolution == null)
            return null;

        var result = new List<TypeHierarchyItem>();
        var symbol = typeHierarchyCache.GetValueOrDefault((int)typeHierarchySubtypesParams.Item.Data.Value);
        if (symbol == null || symbol is not INamedTypeSymbol namedTypeSymbol)
            return null;

        var subtypes = await SymbolFinder.FindDerivedInterfacesAsync(namedTypeSymbol, navigationService.HostSolution, transitive: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var subtype in subtypes)
            result.Add(CreateTypeHierarchyItem(subtype, typeHierarchySubtypesParams.Item));

        subtypes = await SymbolFinder.FindDerivedClassesAsync(namedTypeSymbol, navigationService.HostSolution, transitive: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var subtype in subtypes)
            result.Add(CreateTypeHierarchyItem(subtype, typeHierarchySubtypesParams.Item));

        subtypes = await SymbolFinder.FindImplementationsAsync(namedTypeSymbol, navigationService.HostSolution, transitive: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var subtype in subtypes)
            result.Add(CreateTypeHierarchyItem(subtype, typeHierarchySubtypesParams.Item));

        return result;
    }

    private TypeHierarchyItem CreateTypeHierarchyItem(ISymbol symbol, TextDocumentPositionParams fallbackParams) {
        return CreateTypeHierarchyItem(symbol, fallbackParams.TextDocument.Uri.FileSystemPath, fallbackParams.Position.ToRange());
    }
    private TypeHierarchyItem CreateTypeHierarchyItem(ISymbol symbol, TypeHierarchyItem fallbackItem) {
        return CreateTypeHierarchyItem(symbol, fallbackItem.Uri.FileSystemPath, default(DocumentRange));
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