using DotRush.Common.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Definition;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis.FindSymbols;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class DefinitionHandler : DefinitionHandlerBase {
    private readonly NavigationService navigationService;

    public DefinitionHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.DefinitionProvider = true;
    }
    protected override Task<DefinitionResponse?> Handle(DefinitionParams request, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync<DefinitionResponse?>(async () => {
            var documentIds = navigationService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath);
            if (documentIds == null)
                return null;

            var result = new HashSet<Location>();
            foreach (var documentId in documentIds) {
                var document = navigationService.Solution?.GetDocument(documentId);
                if (document == null)
                    continue;

                var sourceText = await document.GetTextAsync(cancellationToken);
                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
                if (symbol == null)
                    continue;

                var definitionSpans = await navigationService.FindDefinitionsAsync(symbol, document.Project, cancellationToken);
                result.AddRange(definitionSpans.Select(x => x.ToLocation()));
            }

            return new DefinitionResponse(result.ToList());
        });
    }
}
