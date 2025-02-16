using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Reference;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis.FindSymbols;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class ReferenceHandler : ReferenceHandlerBase {
    private readonly NavigationService navigationService;

    public ReferenceHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.ReferencesProvider = true;
    }
    protected override async Task<ReferenceResponse?> Handle(ReferenceParams request, CancellationToken cancellationToken) {
        var documentIds = navigationService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath);
        if (documentIds == null || navigationService.Solution == null)
            return null;

        var result = new List<Location>();
        foreach (var documentId in documentIds) {
            var document = navigationService.Solution.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
            if (symbol == null || symbol.Locations == null)
                continue;

            var referenceSymbols = await SymbolFinder.FindReferencesAsync(symbol, navigationService.Solution, cancellationToken);
            var referenceLocations = referenceSymbols
                .SelectMany(r => r.Locations)
                .Where(l => File.Exists(l.Document.FilePath));

            foreach (var location in referenceLocations) {
                var locationSourceText = await location.Document.GetTextAsync(cancellationToken);
                var referenceLocation = location.ToLocation(locationSourceText);
                if (referenceLocation != null)
                    result.Add(referenceLocation.Value);
            }
        }

        return new ReferenceResponse(result);
    }
}