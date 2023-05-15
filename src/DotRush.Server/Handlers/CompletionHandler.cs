using System.Text;
using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using CodeAnalysisCompletionItem = Microsoft.CodeAnalysis.Completion.CompletionItem;
using CodeAnalysisCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace DotRush.Server.Handlers;

public class CompletionHandler : CompletionHandlerBase {
    private readonly Dictionary<int, CodeAnalysisCompletionItem> cachedItems;
    private readonly SolutionService solutionService;
    private Document? targetDocument;

    public CompletionHandler(SolutionService solutionService) {
        this.cachedItems = new Dictionary<int, CodeAnalysisCompletionItem>();
        this.solutionService = solutionService;
    }

    protected override CompletionRegistrationOptions CreateRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities) {
        return new CompletionRegistrationOptions {
            TriggerCharacters = new[] { ".", ":", " ", "(", "$" },
            AllCommitCharacters = new[] { "(", "." },
            ResolveProvider = true,
        };
    }

    public override async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken) {
        var completionItems = new List<CompletionItem>();
        var documentId = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath()).FirstOrDefault();
        var document = this.solutionService.Solution?.GetDocument(documentId);
        var completionService = CodeAnalysisCompletionService.GetService(document);
        if (completionService == null || document == null) 
            return new CompletionList(completionItems);

        var sourceText = await document.GetTextAsync(cancellationToken);
        var position = request.Position.ToOffset(sourceText);
        var completions = await completionService.GetCompletionsAsync(document, position, cancellationToken: cancellationToken);
        if (completions == null)
            return new CompletionList(completionItems);
        
        AssignCacheWithDocument(document);
        return new CompletionList(completions.ItemsList.Select(x => {
            var item = x.ToCompletionItem();
            CacheCompletionItem(item, x);
            return item;
        }));
    }

    public override async Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken) {
        if (this.targetDocument == null || request.Data == null) 
            return request;

        StringOrMarkupContent? documentation = null;
        IEnumerable<TextEdit>? additionalTextEdits = null;
        TextEdit? textEdit = null;

        var id = request.Data.ToObject<int>();
        var completionService = CodeAnalysisCompletionService.GetService(this.targetDocument);

        if (request.Documentation == null) {
            if (cachedItems.TryGetValue(id, out var item) && completionService != null) {
                var description = await completionService.GetDescriptionAsync(this.targetDocument, item, cancellationToken);
                if (description != null) {
                    var stringBuilder = new StringBuilder();
                    MarkdownConverter.TaggedTextToMarkdown(description.TaggedParts, stringBuilder);
                    documentation = new StringOrMarkupContent(new MarkupContent() {
                        Kind = MarkupKind.Markdown,
                        Value = stringBuilder.ToString()
                    });
                }
            }
        }

        if (request.TextEdit == null) {
            if (cachedItems.TryGetValue(id, out var item) && completionService != null) {
                var changes = await completionService.GetChangeAsync(this.targetDocument, item, cancellationToken: cancellationToken);
                if (changes != null && item.IsComplexTextEdit) {
                    var sourceText = await this.targetDocument.GetTextAsync(cancellationToken);
                    textEdit = changes.TextChange.ToEmptyTextEdit();
                    additionalTextEdits = changes.TextChanges
                        .Select(change => change.ToTextEdit(sourceText));
                } 
            }
        }

        return new CompletionItem() {
            Label = request.Label,
            SortText = request.SortText,
            FilterText = request.FilterText,
            Detail = request.Detail,
            Data = request.Data,
            Kind = request.Kind,
            Preselect = request.Preselect,
            Documentation = documentation,
            AdditionalTextEdits = additionalTextEdits != null ? new TextEditContainer(additionalTextEdits) : null,
            TextEdit = textEdit != null ? new TextEditOrInsertReplaceEdit(textEdit) : null,
        };
    }


    private void AssignCacheWithDocument(Document document) {
        this.targetDocument = document;
        this.cachedItems.Clear();
    }
    private void CacheCompletionItem(CompletionItem completionItem, CodeAnalysisCompletionItem codeAnalysisCompletionItem) {
        if (completionItem.Data == null)
            return;
        
        var id = completionItem.Data.ToObject<int>();
        this.cachedItems.TryAdd(id, codeAnalysisCompletionItem);
    }
}