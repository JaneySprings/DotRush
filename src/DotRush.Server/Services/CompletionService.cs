using System.Text;
using DotRush.Server.Extensions;
using LanguageServer.Parameters.TextDocument;
using Microsoft.CodeAnalysis;

namespace DotRush.Server.Services;

public class CompletionService {
    public static CompletionService Instance { get; private set; } = null!;
    private Dictionary<int, Microsoft.CodeAnalysis.Completion.CompletionItem> cachedItems = new();
    public Document? TargetDocument { get; private set; }

    private CompletionService() {}

    public static void Initialize() {
        var service = new CompletionService();
        Instance = service;
    }

    public CompletionResult GetCompletionItems(CompletionParams @params) {
        var completionItems = new List<CompletionItem>();
        var document = DocumentService.GetDocumentByPath(@params.textDocument.uri.ToSystemPath());
        var completionService = Microsoft.CodeAnalysis.Completion.CompletionService.GetService(document);
        if (completionService == null || document == null) 
            return new CompletionResult(completionItems.ToArray());

        var position = @params.position.ToOffset(document);
        var completions = completionService.GetCompletionsAsync(document, position).Result;
        if (completions == null)
            return new CompletionResult(completionItems.ToArray());

        AssignCacheWithDocument(document);
        return new CompletionResult(
            completions.ItemsList.Select(item => {
                var completionItem = item.ToCompletionItem();
                CacheCompletionItem(completionItem, item);
                return completionItem;
            }).ToArray()
        );
    }
    public void ResolveItemDocumentation(CompletionItem @params) {
        if (TargetDocument == null || @params.documentation != null) 
            return;

        var completionService = Microsoft.CodeAnalysis.Completion.CompletionService.GetService(TargetDocument);
        if (cachedItems.TryGetValue((int)@params.data, out var item) && completionService != null) {
            var description = completionService?.GetDescriptionAsync(TargetDocument, item).Result;
            if (description == null) 
                return;

            var stringBuilder = new StringBuilder();
            MarkdownConverter.TaggedTextToMarkdown(description.TaggedParts, stringBuilder);
            @params.documentation = new MarkupContent() {
                kind = "markdown",
                value = stringBuilder.ToString()
            };
        }
    }
    public void ResolveItemChanges(CompletionItem @params) {
        if (TargetDocument == null || @params.textEdit != null) 
            return;

        var completionService = Microsoft.CodeAnalysis.Completion.CompletionService.GetService(TargetDocument);
        if (cachedItems.TryGetValue((int)@params.data, out var item) && completionService != null) {
            var changes = completionService?.GetChangeAsync(TargetDocument, item).Result;
            if (changes == null || !item.IsComplexTextEdit) 
                return;
            // Hack for strange behavior of LSP client
            @params.textEdit = changes.TextChange.ToEmptyTextEdit();
            @params.additionalTextEdits = changes.TextChanges
                .Select(change => change.ToTextEdit(TargetDocument))
                .ToArray();
        }
    }

    private void AssignCacheWithDocument(Document document) {
        TargetDocument = document;
        cachedItems.Clear();
    }
    private void CacheCompletionItem(CompletionItem @params, Microsoft.CodeAnalysis.Completion.CompletionItem item) {
        cachedItems.TryAdd((int)@params.data, item);
    }
}