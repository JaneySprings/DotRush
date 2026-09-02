using DotRush.Common.Extensions;
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
    protected override Task<ReferenceResponse?> Handle(ReferenceParams request, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync<ReferenceResponse?>(async () => {
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

            return new ReferenceResponse(result.ToList());
        });
    }
}
