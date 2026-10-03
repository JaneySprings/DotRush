using DotRush.Common.Extensions;
using DotRush.Protocol.Handlers;
using DotRush.Protocol.Models;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis.FindSymbols;
using ProtocolModels = DotRush.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class TypeDefinitionHandler : TypeDefinitionHandlerBase {
    private readonly NavigationService navigationService;

    public TypeDefinitionHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities) {
        serverCapabilities.TypeDefinitionProvider = true;
    }
    protected override async Task<List<ProtocolModels.Location>?> Handle(TypeDefinitionParams request, CancellationToken cancellationToken) {
        var documentPath = request.TextDocument.Uri.FileSystemPath;
        var solution = navigationService.GetRequiredSolution(documentPath);
        var documentIds = solution?.GetDocumentIdsWithFilePathV2(documentPath);
        if (documentIds == null)
            return null;

        var result = new HashSet<ProtocolModels.Location>();
        foreach (var documentId in documentIds) {
            var document = solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
            if (symbol == null)
                continue;

            var typeSymbol = symbol.GetTypeSymbol();
            if (typeSymbol == null)
                continue;

            var definitionSpans = await navigationService.FindDefinitionsAsync(typeSymbol, document.Project, cancellationToken);
            result.AddRange(definitionSpans.Select(x => x.ToLocation()));
        }

        return result.ToList();
    }
}
