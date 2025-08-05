using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis.Reflection;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.FoldingRange;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class FoldingRangeHandler : FoldingRangeHandlerBase {
    private readonly NavigationService navigationService;
    private object? blockStructureService;

    public FoldingRangeHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.FoldingRangeProvider = true;
    }
    protected override Task<FoldingRangeResponse> Handle(FoldingRangeParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(new FoldingRangeResponse(new List<FoldingRange>()), async () => {
            var result = new List<FoldingRange>();

            var documentIds = navigationService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath);
            var documentId = documentIds?.FirstOrDefault();
            var document = navigationService.Solution?.GetDocument(documentId);
            if (document == null)
                return new FoldingRangeResponse(result);

            if (blockStructureService == null)
                blockStructureService = InternalCSharpBlockStructureService.CreateNew(document.Project.Solution.Services);

            var blockStructure = await InternalCSharpBlockStructureService.GetBlockStructureAsync(blockStructureService, document, InternalBlockStructureOptions.Default, token).ConfigureAwait(false);
            var spans = InternalBlockStructure.GetSpans(blockStructure);
            if (spans == null)
                return new FoldingRangeResponse(result);

            var sourceText = await document.GetTextAsync(token).ConfigureAwait(false);
            foreach (var span in spans) {
                var bannerText = InternalBlockStructure.GetBannerText(span);
                var textSpan = InternalBlockStructure.GetTextSpan(span).ToRange(sourceText);
                result.Add(new FoldingRange {
                    StartLine = (uint)textSpan.Start.Line,
                    StartCharacter = (uint)textSpan.Start.Character,
                    EndLine = (uint)textSpan.End.Line,
                    EndCharacter = (uint)textSpan.End.Character,
                    CollapsedText = bannerText,
                });
            }

            return new FoldingRangeResponse(result);
        });
    }
}