using System.Text;
using dotRush.Server.Extensions;
using LanguageServer.Parameters.TextDocument;
using Microsoft.CodeAnalysis;

namespace dotRush.Server.Services;

public class DocumentationService {
    public static DocumentationService Instance { get; private set; } = null!;
    private Dictionary<CompletionItem, Microsoft.CodeAnalysis.Completion.CompletionItem> cachedItems = new();
    public Document? TargetDocument { get; private set; }

    private DocumentationService() {}

    public static void Initialize() {
        var service = new DocumentationService();
        Instance = service;
    }

    public void AssignCacheWithDocument(Document document) {
        TargetDocument = document;
        cachedItems.Clear();
    }

    public void CacheCompletionItem(CompletionItem @params, Microsoft.CodeAnalysis.Completion.CompletionItem item) {
        cachedItems.TryAdd(@params, item);
    }

    public void ResolveCompletionItem(CompletionItem @params) {
        if (TargetDocument == null || @params.documentation != null) 
            return;

        var completionService = Microsoft.CodeAnalysis.Completion.CompletionService.GetService(TargetDocument);
        var cachedItem = this.cachedItems.FirstOrDefault(x => x.Key.label == @params.label).Value;
        if (cachedItem != null && completionService != null) {
            var description = completionService.GetDescriptionAsync(TargetDocument, cachedItem).Result;
            var stringBuilder = new StringBuilder();
            MarkdownConverter.TaggedTextToMarkdown(description!.TaggedParts, stringBuilder, MarkdownFormat.FirstLineAsCSharp);
            @params.documentation = new MarkupContent() {
                kind = "markdown",
                value = stringBuilder.ToString()
            };
        }
    }
}