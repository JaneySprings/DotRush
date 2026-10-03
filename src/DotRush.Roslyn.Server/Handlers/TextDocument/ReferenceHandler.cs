using DotRush.Common.Extensions;
using DotRush.Protocol.Handlers;
using DotRush.Protocol.Models;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis.FindSymbols;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class ReferenceHandler : ReferenceHandlerBase {
    private readonly NavigationService navigationService;

    public ReferenceHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities) {
        serverCapabilities.ReferencesProvider = true;
    }
    protected override async Task<List<Location>?> Handle(ReferenceParams request, CancellationToken cancellationToken) {
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

            var referenceSpans = await navigationService.FindReferencesAsync(symbol, cancellationToken);
            result.AddRange(referenceSpans.Select(x => x.ToLocation()));
        }

        return result.ToList();
    }
}
