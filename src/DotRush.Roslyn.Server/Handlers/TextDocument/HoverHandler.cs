using DotRush.Protocol.Handlers;
using DotRush.Protocol.Models;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.QuickInfo;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class HoverHandler : HoverHandlerBase {
    private readonly NavigationService navigationService;

    public HoverHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities) {
        serverCapabilities.HoverProvider = true;
    }
    protected override async Task<Hover?> Handle(HoverParams request, CancellationToken token) {
        var documentPath = request.TextDocument.Uri.FileSystemPath;
        var solution = navigationService.GetRequiredSolution(documentPath);
        var documentIds = solution?.GetDocumentIdsWithFilePathV2(documentPath);
        if (documentIds == null)
            return null;

        var displayDictionary = new Dictionary<string, List<string>>();
        var result = new Hover();
        foreach (var documentId in documentIds) {
            var document = solution?.GetDocument(documentId);
            var quickInfoService = QuickInfoService.GetService(document);
            if (document == null || quickInfoService == null)
                continue;

            var sourceText = await document.GetTextAsync(token);
            var offset = request.Position.ToOffset(sourceText);
            var quickInfo = await quickInfoService.GetQuickInfoAsync(document, offset, token);
            if (quickInfo == null)
                continue;

            var displayString = MarkdownExtensions.CreateDocumentation(quickInfo.Sections.SelectMany(x => x.TaggedParts.Add(new TaggedText(TextTags.LineBreak, Environment.NewLine))));
            if (!displayDictionary.ContainsKey(displayString))
                displayDictionary[displayString] = new List<string>();

            displayDictionary[displayString].Add(document.Project.GetTargetFramework());
            result.Range ??= quickInfo.Span.ToRange(sourceText);
        }

        if (displayDictionary.Count == 0)
            return null;
        if (displayDictionary.Count == 1) {
            result.Contents = new MarkupContent { Kind = MarkupKind.Markdown, Value = displayDictionary.Keys.First() };
            return result;
        }

        result.Contents = new MarkupContent {
            Kind = MarkupKind.Markdown,
            Value = string.Concat(displayDictionary.Select(kv => MarkdownExtensions.InjectText($"({string.Join(", ", kv.Value)}): ", kv.Key)))
        };
        return result;
    }
}