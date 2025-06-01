using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.Options;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Completion;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Kind;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Markup;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Union;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Model;
using RoslynCompletionItem = Microsoft.CodeAnalysis.Completion.CompletionItem;
using RoslynCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class CompletionHandler : CompletionHandlerBase {
    private readonly WorkspaceService workspaceService;
    private readonly ConfigurationService configurationService;
    private readonly CurrentClassLogger currentClassLogger;

    private Dictionary<int, RoslynCompletionItem>? completionItemsCache;
    private DocumentId? documentId;

    public CompletionHandler(WorkspaceService workspaceService, ConfigurationService configurationService) {
        this.workspaceService = workspaceService;
        this.configurationService = configurationService;
        this.currentClassLogger = new CurrentClassLogger(nameof(CompletionHandler));
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.CompletionProvider = new CompletionOptions {
            TriggerCharacters = new List<string> { ".", " ", "(", "<" },
            ResolveProvider = true,
        };
    }
    protected override Task<CompletionResponse?> Handle(CompletionParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(async () => {
            documentId = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath).FirstOrDefault();
            var document = workspaceService.Solution?.GetDocument(documentId);
            var completionService = RoslynCompletionService.GetService(document);
            if (completionService == null || document == null)
                return null;

            var sourceText = await document.GetTextAsync(token).ConfigureAwait(false);
            var offset = request.Position.ToOffset(sourceText);
            var completions = await completionService.GetCompletionsAsync(document, offset, configurationService, token).ConfigureAwait(false);

            var completionItems = new List<CompletionItem>();
            completionItemsCache = new Dictionary<int, RoslynCompletionItem>(completions.ItemsList.Count);
            foreach (var item in completions.ItemsList) {
                var id = item.GetHashCode();
                completionItems.Add(new CompletionItem() {
                    Data = id,
                    Label = item.DisplayTextPrefix + item.DisplayText + item.DisplayTextSuffix,
                    FilterText = item.FilterText,
                    Detail = item.InlineDescription,
                    InsertTextMode = InsertTextMode.AsIs,
                    Kind = item.ToCompletionItemKind(),
                    Preselect = item.Rules.MatchPriority == Microsoft.CodeAnalysis.Completion.MatchPriority.Preselect,
                    SortText = item.HasPriority() ? $"0_{item.SortText}" : item.SortText,
                    Deprecated = item.Tags.Contains(InternalWellKnownTags.Deprecated),
                });
                completionItemsCache.TryAdd(id, item);
            }
            return new CompletionResponse(completionItems);
        });
    }
    protected override async Task<CompletionItem> Resolve(CompletionItem item, CancellationToken token) {
        if (documentId == null || item.Data?.Value == null || completionItemsCache == null)
            return item;

        if (!completionItemsCache.TryGetValue((int)item.Data.Value, out var roslynCompletionItem)) {
            CurrentSessionLogger.Debug($"Completion item with data:[{item.Data.Value}] not found in cache.");
            return item;
        }

        var document = workspaceService.Solution?.GetDocument(documentId);
        if (document == null || workspaceService.Solution == null) {
            CurrentSessionLogger.Debug($"Document with identifier:[{documentId}] not found.");
            return item;
        }

        var completionService = RoslynCompletionService.GetService(document);
        if (completionService == null) {
            CurrentSessionLogger.Debug($"Roslyn completion service not found for document:[{documentId}].");
            return item;
        }

        if (item.Documentation == null) {
            var description = await completionService.GetDescriptionAsync(document, roslynCompletionItem, token).ConfigureAwait(false);
            if (description != null) {
                item.Documentation = new MarkupContent() {
                    Kind = MarkupKind.Markdown,
                    Value = MarkdownConverter.TaggedTextToMarkdown(description.TaggedParts)
                };
            }
        }

        var changes = await completionService.GetChangeAsync(document, roslynCompletionItem, cancellationToken: token).ConfigureAwait(false);
        var sourceText = await document.GetTextAsync(token).ConfigureAwait(false);
        if (changes?.TextChanges == null) {
            currentClassLogger.Debug($"No text changes found for item:[{item.Label}]");
            return item;
        }

        (item.TextEdit, item.AdditionalTextEdits) = ArrangeTextEdits(changes.TextChanges, roslynCompletionItem.Span, sourceText);
        return item;
    }

    private static (TextEditOrInsertReplaceEdit, List<AnnotatedTextEdit>) ArrangeTextEdits(IEnumerable<TextChange> changes, TextSpan completionSpan, SourceText sourceText) {
        var additionalTextEdits = changes
            .Where(x => !x.Span.IntersectsWith(completionSpan))
            .Select(x => x.ToAnnotatedTextEdit(sourceText))
            .ToList();

        var textEdit = changes
            .FirstOrDefault(x => x.Span.IntersectsWith(completionSpan))
            .ToTextEdit(sourceText);

        // Remove previous text behind the cursor position manualy
        // because fucking vscode bydesign
        additionalTextEdits.Add(new ProtocolModels.TextEdit.AnnotatedTextEdit() {
            NewText = string.Empty,
            Range = new ProtocolModels.DocumentRange() {
                Start = textEdit.Range.Start,
                End = completionSpan.Start.ToPosition(sourceText)
            }
        });

        return (new TextEditOrInsertReplaceEdit(textEdit), additionalTextEdits);
    }
}