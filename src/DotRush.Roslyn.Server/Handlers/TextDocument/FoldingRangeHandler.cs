using DotRush.Protocol.Handlers;
using DotRush.Protocol.Models;
using DotRush.Roslyn.CodeAnalysis.Reflection;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class FoldingRangeHandler : FoldingRangeHandlerBase {
    private readonly NavigationService navigationService;
    private object? blockStructureService;

    public FoldingRangeHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities) {
        serverCapabilities.FoldingRangeProvider = true;
    }
    protected override async Task<List<FoldingRange>> Handle(FoldingRangeParams request, CancellationToken token) {
        var result = new List<FoldingRange>();

        var documentPath = request.TextDocument.Uri.FileSystemPath;
        var solution = navigationService.GetRequiredSolution(documentPath);
        var documentIds = solution?.GetDocumentIdsWithFilePathV2(documentPath);
        var documentId = documentIds?.FirstOrDefault();
        var document = solution?.GetDocument(documentId);
        if (document == null)
            return result;

        if (blockStructureService == null)
            blockStructureService = InternalCSharpBlockStructureService.CreateNew(document.Project.Solution.Services);

        var blockStructure = await InternalCSharpBlockStructureService.GetBlockStructureAsync(blockStructureService, document, InternalBlockStructureOptions.Default, token).ConfigureAwait(false);
        var spans = InternalBlockStructure.GetSpans(blockStructure);
        if (spans == null)
            return result;

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

        return result;
    }
}