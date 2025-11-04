using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Reflection;
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
using RoslynCompletionItem = Microsoft.CodeAnalysis.Completion.CompletionItem;
using RoslynCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class CompletionHandler : CompletionHandlerBase {
    private readonly WorkspaceService workspaceService;
    private readonly ConfigurationService configurationService;
    private readonly CurrentClassLogger currentClassLogger;

    private Dictionary<int, RoslynCompletionItem>? completionItemsCache;
    private RoslynCompletionService? completionService;
    private DocumentId? documentId;
    private int offset;

    public CompletionHandler(WorkspaceService workspaceService, ConfigurationService configurationService) {
        this.workspaceService = workspaceService;
        this.configurationService = configurationService;
        this.currentClassLogger = new CurrentClassLogger(nameof(CompletionHandler));
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.CompletionProvider = new CompletionOptions {
            TriggerCharacters = new List<string> { " ", ".", "#", ">", ":" },
            ResolveProvider = true,
        };
    }
    protected override Task<CompletionResponse?> Handle(CompletionParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(async () => {
            if (request.Context.TriggerKind == CompletionTriggerKind.TriggerCharacter && !configurationService.TriggerCompletionOnSpace) {
                if (request.Context.TriggerCharacter == " ")
                    return null;
            }

            documentId = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath).FirstOrDefault();
            var document = workspaceService.Solution?.GetDocument(documentId);
            if (completionService == null)
                completionService = RoslynCompletionService.GetService(document);
            if (completionService == null || document == null)
                return null;

            var sourceText = await document.GetTextAsync(token).ConfigureAwait(false);
            offset = request.Position.ToOffset(sourceText);
            var typedSpan = completionService.GetDefaultCompletionListSpan(sourceText, offset);
            var completions = await completionService.GetCompletionsAsync(document, offset, configurationService, token).ConfigureAwait(false);

            var completionItems = new List<CompletionItem>();
            completionItemsCache = new Dictionary<int, RoslynCompletionItem>(completions.ItemsList.Count);
            foreach (var item in completions.ItemsList) {
                var id = item.GetHashCode();
                var completionItem = new CompletionItem() {
                    Data = id,
                    Label = item.DisplayTextPrefix + item.DisplayText + item.DisplayTextSuffix,
                    FilterText = item.FilterText,
                    Detail = item.InlineDescription,
                    InsertTextMode = InsertTextMode.AsIs,
                    Kind = item.ToCompletionItemKind(),
                    Preselect = item.Rules.MatchPriority == Microsoft.CodeAnalysis.Completion.MatchPriority.Preselect,
                    SortText = item.HasPriority() ? $"0_{item.SortText}" : item.SortText,
                    Deprecated = item.Tags.Contains(InternalWellKnownTags.Deprecated),
                };

                if (item.ShouldResolveImmediately())
                    await ResolveComplexItemAsync(completionService, item, completionItem, offset, document, sourceText, token).ConfigureAwait(false);

                completionItems.Add(completionItem);
                completionItemsCache.TryAdd(id, item);
            }

            return new CompletionResponse(completionItems);
        });
    }
    protected override Task<CompletionItem> Resolve(CompletionItem item, CancellationToken token) {
        return SafeExtensions.InvokeAsync<CompletionItem>(item, async () => {
            if (documentId == null || item.Data?.Value == null || completionItemsCache == null)
                return item;
            if (!completionItemsCache.TryGetValue((int)item.Data.Value, out var roslynCompletionItem))
                return item;

            var document = workspaceService.Solution?.GetDocument(documentId);
            if (completionService == null || document == null) {
                currentClassLogger.Debug($"Roslyn completion service not found for document:[{document}].");
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

            if (item.TextEdit == null && roslynCompletionItem.IsComplexTextEdit) {
                var sourceText = await document.GetTextAsync(token).ConfigureAwait(false);
                await ResolveComplexItemAsync(completionService, roslynCompletionItem, item, offset, document, sourceText, token).ConfigureAwait(false);
            }

            return item;
        });
    }

    //https://github.com/OmniSharp/omnisharp-roslyn/blob/c38e89b04a97ec8bc488926ef2f501d7401c4b33/src/OmniSharp.Roslyn.CSharp/Services/Completion/CompletionListBuilder_Sync.cs#L135
    private static async Task<CompletionItem> ResolveComplexItemAsync(RoslynCompletionService completionService, RoslynCompletionItem completionItem, CompletionItem item, int offset, Document document, SourceText sourceText, CancellationToken token) {
        var completionChange = await completionService.GetChangeAsync(document, completionItem, cancellationToken: token).ConfigureAwait(false);
        if (completionChange == null)
            return item;

        item.InsertTextFormat = InsertTextFormat.PlainText;
        var additionalTextEdits = new List<AnnotatedTextEdit>();
        var typedSpan = completionService.GetDefaultCompletionListSpan(sourceText, offset);
        var changeSpan = typedSpan;

        var adjustedNewPosition = completionChange.NewPosition;
        var cursorPoint = offset.ToPosition(sourceText);
        var lineStartPosition = sourceText.Lines[cursorPoint.Line].Start;
        foreach (var textChange in completionChange.TextChanges) {
            if (!textChange.Span.IntersectsWith(offset)) {
                HandleNonInsertsectingEdit(sourceText, additionalTextEdits, ref adjustedNewPosition, textChange);
            }
            else {
                // Either there should be no new position, or it should be within the text that is being added
                // by this change.
                var changeSpanStart = textChange.Span.Start;

                // Filtering needs a range that is a _single_ line. Consider a case like this (whitespace documented with escapes):
                //
                // 1: class C
                // 2: {\t\r\n
                // 3:    override $$
                //
                // Roslyn will see the trailing \t on line 2 and remove it when creating the _main_ text change. However, that will
                // break filtering because filtering expects a single line as part of the range. So what we want to do is break the
                // the text change up into two: one to cover the previous line, as an additional edit, and then one to cover the
                // rest of the change.

                var updatedChange = textChange;
                if (changeSpanStart < lineStartPosition) {
                    // We know we're in the special case. In order to correctly determine the amount of leading newlines to trim, we want
                    // to calculate the number of lines before the cursor we're editing
                    var editStartPoint = changeSpanStart.ToPosition(sourceText);
                    var numLinesEdited = cursorPoint.Line - editStartPoint.Line;

                    // Now count that many newlines forward in the edited text
                    var cutoffPosition = 0;
                    for (int numNewlinesFound = 0; numNewlinesFound < numLinesEdited; cutoffPosition++) {
                        if (textChange.NewText![cutoffPosition] == '\n') {
                            numNewlinesFound++;
                        }
                    }

                    // Now that we've found the cuttoff, we can build our two subchanges
                    var prefixChange = new TextChange(new TextSpan(changeSpanStart, length: lineStartPosition - changeSpanStart), textChange.NewText!.Substring(0, cutoffPosition));
                    HandleNonInsertsectingEdit(sourceText, additionalTextEdits, ref adjustedNewPosition, prefixChange);
                    updatedChange = new TextChange(new TextSpan(lineStartPosition, length: textChange.Span.End - lineStartPosition), textChange.NewText.Substring(cutoffPosition));
                }

                changeSpan = updatedChange.Span;
                // When inserting at the beginning or middle of a word, we want to only replace characters
                // up until the caret position, but not after.  For example when typing at the beginning of a word
                // we only want to insert the completion before the rest of the word.
                // However, Roslyn returns the entire word as the span to replace, so we have to adjust it.
                if (offset < changeSpan.End) {
                    changeSpan = new(changeSpan.Start, length: offset - changeSpan.Start);
                }

                (item.InsertText, item.InsertTextFormat) = CreateInsertTextWithSnippets(updatedChange, adjustedNewPosition);

                // If the completion is replacing a bigger range than the previously-typed word, we need to have the filter
                // text compensate. Clients will use the range of the text edit to determine the thing that is being filtered
                // against. For example, override completion:
                //
                //    override $$
                //    |--------| Range that is being changed by the completion
                //
                // That means vscode will consider "override <additional user input>" when looking to see whether the item
                // still matches. To compensate, we add the start of the replacing range, up to the start of the current word,
                // to ensure the item isn't silently filtered out.

                if (changeSpan != typedSpan) {
                    if (typedSpan.Start < changeSpan.Start) {
                        // This means that some part of the currently-typed text is an exact match for the start of the
                        // change, so chop off changeSpan.Start - typedSpan.Start from the filter text to get it to match
                        // up with the range
                        int prefixMatchElement = changeSpan.Start - typedSpan.Start;
                        item.FilterText = completionItem.FilterText.Substring(prefixMatchElement);
                    }
                    else {
                        var prefix = sourceText.GetSubText(TextSpan.FromBounds(changeSpan.Start, typedSpan.Start)).ToString();
                        item.FilterText = prefix + completionItem.FilterText;
                    }
                }
                else {
                    item.FilterText = item.Label == completionItem.FilterText ? null : completionItem.FilterText;
                }
            }
        }

        item.AdditionalTextEdits = additionalTextEdits;
        item.TextEdit = new TextEditOrInsertReplaceEdit(new TextEdit {
            NewText = item.InsertText ?? string.Empty,
            Range = changeSpan.ToRange(sourceText)
        });
        return item;
    }
    private static (string?, InsertTextFormat) CreateInsertTextWithSnippets(TextChange change, int? adjustedNewPosition) {
        if (adjustedNewPosition is not int newPosition || change.NewText is null || newPosition == change.Span.Start + change.NewText.Length)
            return (change.NewText, InsertTextFormat.PlainText);

        // Roslyn wants to move the cursor somewhere inside the result. Substring from the
        // requested start to the new position, and from the new position to the end of the
        // string.
        int midpoint = newPosition - change.Span.Start;
        var beforeText = CompletionExtensions.Escape(change.NewText.Substring(0, midpoint));
        var afterText = CompletionExtensions.Escape(change.NewText.Substring(midpoint));
        return ($"{beforeText}$0{afterText}", InsertTextFormat.Snippet);
    }
    private static void HandleNonInsertsectingEdit(SourceText sourceText, List<AnnotatedTextEdit> additionalTextEdits, ref int? adjustedNewPosition, TextChange textChange) {
        additionalTextEdits.Add(textChange.ToAnnotatedTextEdit(sourceText));

        if (adjustedNewPosition is not int newPosition)
            return;

        // Find the diff between the original text length and the new text length.
        var diff = (textChange.NewText?.Length ?? 0) - textChange.Span.Length;

        // If the new text is longer than the replaced text, we want to subtract that
        // length from the current new position to find the adjusted position in the old
        // document. If the new text was shorter, diff will be negative, and subtracting
        // will result in increasing the adjusted position as expected
        adjustedNewPosition = newPosition - diff;
    }
}
