using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis.Reflection;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.Options;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Completion;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Kind;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Markup;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using RoslynCompletionItem = Microsoft.CodeAnalysis.Completion.CompletionItem;
using RoslynCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class CompletionV2Handler : CompletionHandlerBase {
    private readonly WorkspaceService workspaceService;
    private readonly ConfigurationService configurationService;

    private Dictionary<int, RoslynCompletionItem> completionItemsCache;
    // VSCode calls `handle` method for the first typed char, and then filter completions without
    // updating current cache. Store this state of document for correct textEdit calculation in `resolve`
    private Document? document;
    private int cursorOffset;

    public CompletionV2Handler(WorkspaceService workspaceService, ConfigurationService configurationService) {
        this.workspaceService = workspaceService;
        this.configurationService = configurationService;
        this.completionItemsCache = new Dictionary<int, RoslynCompletionItem>();
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.CompletionProvider = new CompletionOptions {
            TriggerCharacters = new List<string> { " ", ".", "#", ">", ":" },
            ResolveProvider = true,
        };
    }
    protected override Task<CompletionResponse?> Handle(CompletionParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(async () => {
            if (request.Context?.TriggerKind == CompletionTriggerKind.TriggerCharacter && !configurationService.TriggerCompletionOnSpace) {
                if (request.Context.TriggerCharacter == " ")
                    return null;
            }

            var documentId = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath).FirstOrDefault();
            document = workspaceService.Solution?.GetDocument(documentId);
            var completionService = RoslynCompletionService.GetService(document);
            if (completionService == null || document == null)
                return null;

            var sourceText = await document.GetTextAsync(token).ConfigureAwait(false);
            cursorOffset = request.Position.ToOffset(sourceText);
            completionItemsCache.Clear();

            var completions = await completionService.GetCompletionsAsync(document, cursorOffset, configurationService, token).ConfigureAwait(false);
            var completionItems = completions.ItemsList.Select(item => {
                var id = item.GetHashCode();
                var completionItem = new CompletionItem() {
                    Data = id,
                    Label = item.DisplayTextPrefix + item.DisplayText + item.DisplayTextSuffix,
                    Kind = item.ToCompletionItemKind(),
                    SortText = item.HasPriority() ? $"0_{item.SortText}" : item.SortText,
                    FilterText = item.FilterText,
                    InsertTextFormat = item.IsSnippet() ? InsertTextFormat.Snippet : InsertTextFormat.PlainText,
                    Detail = item.InlineDescription,
                    Preselect = item.Rules.MatchPriority == Microsoft.CodeAnalysis.Completion.MatchPriority.Preselect,
                    Deprecated = item.Tags.Contains(InternalWellKnownTags.Deprecated),
                };

                if (item.IsComplexTextEdit && !item.IsAutoUsing())
                    completionItem.TextEditText = sourceText.GetSubText(item.Span).ToString();

                completionItemsCache[id] = item;
                return completionItem;
            });

            return new CompletionResponse(new CompletionList {
                Items = completionItems.ToList(),
                ItemDefaults = new CompletionListItemDefault {
                    InsertTextMode = InsertTextMode.AsIs,
                    EditRange = new CompletionListItemDefaultEditRange(completions.Span.ToRange(sourceText))
                },
            });
        });
    }
    protected override Task<CompletionItem> Resolve(CompletionItem item, CancellationToken token) {
        return SafeExtensions.InvokeAsync<CompletionItem>(item, async () => {
            if (item.Data?.Value == null || completionItemsCache == null)
                return item;
            if (!completionItemsCache.TryGetValue((int)item.Data.Value, out var roslynCompletionItem))
                return item;

            var completionService = RoslynCompletionService.GetService(document);
            if (completionService == null || document == null)
                return item;

            if (item.Documentation == null) {
                var description = await completionService.GetDescriptionAsync(document, roslynCompletionItem, token).ConfigureAwait(false);
                if (description != null) {
                    item.Documentation = new MarkupContent() {
                        Kind = MarkupKind.Markdown,
                        Value = MarkdownConverter.TaggedTextToMarkdown(description.TaggedParts)
                    };
                }
            }
            if (roslynCompletionItem.IsComplexTextEdit && item.Command == null && item.AdditionalTextEdits == null) {
                var completionChange = await completionService.GetChangeAsync(document, roslynCompletionItem, cancellationToken: token).ConfigureAwait(false);
                var sourceText = await document.GetTextAsync(token).ConfigureAwait(false);
                if (completionChange == null || sourceText == null)
                    return item;

                var (textEdit, additionalTextEdits) = completionChange.ToTextChanges(sourceText, cursorOffset);

                item.AdditionalTextEdits = additionalTextEdits;
                if (!roslynCompletionItem.IsAutoUsing()) {
                    item.Command = new Command() {
                        Title = nameof(CompletionV2Handler),
                        Name = $"{Resources.ExtensionId}.{nameof(CompletionHandler).ToCamelCase()}",
                        Arguments = new List<LSPAny> {
                            new LSPAny(document.FilePath),
                            new LSPAny(textEdit),
                            new LSPAny(roslynCompletionItem.IsSnippet()),
                            new LSPAny(completionChange.NewPosition ?? -1)
                        }
                    };
                }
            }
            return item;
        });
    }
}
