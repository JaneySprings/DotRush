using DotRush.Roslyn.Common;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.TypeHierarchy;
using EmmyLua.LanguageServer.Framework.Server.Handler;
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
            if (symbol == null || symbol is not ITypeSymbol typeSymbol)
                return null;
            
            result.Add(CreateTypeHierarchyItem(typeSymbol, typeHierarchyPrepareParams.TextDocument.Uri.FileSystemPath));
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
            result.Add(CreateTypeHierarchyItem(typeSymbol.BaseType, typeHierarchySupertypesParams.Item.Uri.FileSystemPath));
        foreach (var iface in typeSymbol.Interfaces)
            result.Add(CreateTypeHierarchyItem(iface, typeHierarchySupertypesParams.Item.Uri.FileSystemPath));

        return Task.FromResult<TypeHierarchyResponse?>(new TypeHierarchyResponse(result));
    }
    protected override Task<TypeHierarchyResponse?> Handle(TypeHierarchySubtypesParams typeHierarchySubtypesParams, CancellationToken cancellationToken) {
        return Task.FromResult<TypeHierarchyResponse?>(null);
    }

    private TypeHierarchyItem CreateTypeHierarchyItem(ISymbol symbol, string filePath) {
        var key = symbol.ToDisplayString().GetHashCode();
        typeHierarchyCache.TryAdd(key, symbol);
        
        return new TypeHierarchyItem {
            Name = symbol.ToDisplayString(DisplayFormat.Member),
            Kind = symbol.ToSymbolKind(),
            Detail = symbol.ToDisplayString(DisplayFormat.Default),
            Uri = symbol.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? filePath,
            Range = symbol.Locations.FirstOrDefault()?.ToRange() ?? PositionExtensions.EmptyRange,
            SelectionRange = symbol.Locations.FirstOrDefault()?.ToRange() ?? PositionExtensions.EmptyRange,
            Data = key
        };
    }
}