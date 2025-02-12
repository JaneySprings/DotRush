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

[Obsolete("In testing. May not work as expected.")]
public class TypeHierarchyHandler : TypeHierarchyHandlerBase {
    private readonly NavigationService navigationService;
    private Dictionary<int, INamedTypeSymbol> typeHierarchyCache;

    public TypeHierarchyHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
        typeHierarchyCache = new Dictionary<int, INamedTypeSymbol>();
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.TypeHierarchyProvider = true;
    }

    protected override async Task<TypeHierarchyResponse?> Handle(TypeHierarchyPrepareParams typeHierarchyPrepareParams, CancellationToken cancellationToken) {
        typeHierarchyCache.Clear();
        
        var documentId = navigationService.Solution?.GetDocumentIdsWithFilePathV2(typeHierarchyPrepareParams.TextDocument.Uri.FileSystemPath).FirstOrDefault();
        if (documentId == null || navigationService.Solution == null)
            return null;

        var result = new List<TypeHierarchyItem>();
        var document = navigationService.Solution.GetDocument(documentId);
        if (document == null)
            return null;

        var sourceText = await document.GetTextAsync(cancellationToken);
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, typeHierarchyPrepareParams.Position.ToOffset(sourceText), cancellationToken);
        if (symbol == null || symbol is not INamedTypeSymbol namedTypeSymbol)
            return null;

        result.Add(CreateTypeHierarchyItem(namedTypeSymbol, typeHierarchyPrepareParams.TextDocument.Uri.FileSystemPath));
        return new TypeHierarchyResponse(result);
    }

    protected override Task<TypeHierarchyResponse?> Handle(TypeHierarchySupertypesParams typeHierarchySupertypesParams, CancellationToken cancellationToken) {
        if (typeHierarchySupertypesParams.Item.Data?.Value == null)
            return Task.FromResult<TypeHierarchyResponse?>(null);

        var symbol = typeHierarchyCache.GetValueOrDefault((int)typeHierarchySupertypesParams.Item.Data.Value);
        if (symbol == null || symbol.BaseType == null)
            return Task.FromResult<TypeHierarchyResponse?>(null);

        var result = new List<TypeHierarchyItem>();
        result.Add(CreateTypeHierarchyItem(symbol.BaseType, typeHierarchySupertypesParams.Item.Uri.FileSystemPath));
        return Task.FromResult<TypeHierarchyResponse?>(new TypeHierarchyResponse(result));
    }

    protected override Task<TypeHierarchyResponse?> Handle(TypeHierarchySubtypesParams typeHierarchySubtypesParams, CancellationToken cancellationToken) {
        return Task.FromResult<TypeHierarchyResponse?>(null);
    }

    private TypeHierarchyItem CreateTypeHierarchyItem(INamedTypeSymbol namedTypeSymbol, string filePath) {
        var key = namedTypeSymbol.ToDisplayString().GetHashCode();
        typeHierarchyCache.Add(key, namedTypeSymbol);

        return new TypeHierarchyItem {
            Name = namedTypeSymbol.Name,
            Kind = namedTypeSymbol.ToSymbolKind(),
            Detail = namedTypeSymbol.ToDisplayString(),
            Uri = namedTypeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? filePath,
            Range = namedTypeSymbol.Locations.FirstOrDefault()?.ToRange() ?? PositionExtensions.EmptyRange,
            SelectionRange = namedTypeSymbol.Locations.FirstOrDefault()?.ToRange() ?? PositionExtensions.EmptyRange,
            Data = key
        };
    }
}
