using DotRush.Common.Extensions;
using DotRush.Protocol.Handlers;
using DotRush.Protocol.Models;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis.FindSymbols;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class DefinitionHandler : DefinitionHandlerBase {
    private readonly NavigationService navigationService;

    public DefinitionHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities) {
        serverCapabilities.DefinitionProvider = true;
    }
    protected override Task<List<Location>?> Handle(DefinitionParams request, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync<List<Location>?>(async () => {
            var documentPath = request.TextDocument.Uri.FileSystemPath;
            var solution = navigationService.GetRequiredSolution(documentPath);
            var documentIds = solution?.GetDocumentIdsWithFilePathV2(documentPath);
            if (documentIds == null)
                return null;

            var result = new HashSet<Location>();
            foreach (var documentId in documentIds) {
                var document = solution?.GetDocument(documentId);
                if (document == null)
                    continue;

                var sourceText = await document.GetTextAsync(cancellationToken);
                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
                if (symbol == null)
                    continue;

                var definitionSpans = await navigationService.FindDefinitionsAsync(symbol, document.Project, cancellationToken);
                result.AddRange(definitionSpans.Select(x => x.ToLocation()));
            }

            return result.ToList();
        });
    }
}
