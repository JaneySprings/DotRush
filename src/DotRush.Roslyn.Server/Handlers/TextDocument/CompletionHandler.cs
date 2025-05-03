using System.Text;
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
    private readonly WorkspaceService solutionService;
    private readonly ConfigurationService configurationService;
    private IEnumerable<RoslynCompletionItem>? codeAnalysisCompletionItems;
    private RoslynCompletionService? roslynCompletionService;
    private Document? document;

    public CompletionHandler(WorkspaceService solutionService, ConfigurationService configurationService) {
        this.solutionService = solutionService;
        this.configurationService = configurationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.CompletionProvider = new CompletionOptions {
            TriggerCharacters = new List<string> { ".", " ", "(", "<" },
            ResolveProvider = true,
        };
    }
    protected override Task<CompletionResponse?> Handle(CompletionParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(async () => {
            var documentId = solutionService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath).FirstOrDefault();
            document = solutionService.Solution?.GetDocument(documentId);
            roslynCompletionService = RoslynCompletionService.GetService(document);
            if (roslynCompletionService == null || document == null)
                return null;

            var sourceText = await document.GetTextAsync(token).ConfigureAwait(false);
            var offset = request.Position.ToOffset(sourceText);
            var completions = await roslynCompletionService.GetCompletionsAsync(document, offset, configurationService, token).ConfigureAwait(false);

            codeAnalysisCompletionItems = completions.ItemsList;
            return new CompletionResponse(completions.ItemsList.Select(item => new CompletionItem() {
                Label = item.DisplayTextPrefix + item.DisplayText + item.DisplayTextSuffix,
                FilterText = item.FilterText,
                Detail = item.InlineDescription,
                Kind = item.ToCompletionItemKind(),
                Data = item.GetHashCode(),
                SortText = item.HasPriority() ? $"0_{item.SortText}" : item.SortText,
                Deprecated = item.Tags.Contains(InternalWellKnownTags.Deprecated),
            }).ToList());
        });
    }
    protected override async Task<CompletionItem> Resolve(CompletionItem item, CancellationToken token) {
        if (document == null || item.Data?.Value == null || roslynCompletionService == null)
            return item;

        var roslynCompletionItem = codeAnalysisCompletionItems?.FirstOrDefault(x => x.GetHashCode() == (int)item.Data.Value);
        if (roslynCompletionItem == null)
            return item;

        IEnumerable<AnnotatedTextEdit>? additionalTextEdits = null;
        TextEditOrInsertReplaceEdit? currentLineTextEdit = null;
        var documentation = new MarkupContent();

        if (item.Documentation is null) {
            var description = await roslynCompletionService.GetDescriptionAsync(document, roslynCompletionItem, token);
            if (description != null) {
                var stringBuilder = new StringBuilder();
                MarkdownConverter.TaggedTextToMarkdown(description.TaggedParts, stringBuilder);
                documentation = new MarkupContent() {
                    Kind = MarkupKind.Markdown,
                    Value = stringBuilder.ToString()
                };
            }
        }

        if (item.AdditionalTextEdits is null || item.TextEdit is null) {
            var changes = await roslynCompletionService.GetChangeAsync(document, roslynCompletionItem, cancellationToken: token);
            var sourceText = await document.GetTextAsync(token);
            if (changes?.TextChanges == null) {
                CurrentSessionLogger.Debug($"No text changes found for item:[{item.Label}]");
                return item;
            }
            (currentLineTextEdit, additionalTextEdits) = ArrangeTextEdits(changes.TextChanges, roslynCompletionItem, sourceText);
        }

        return new CompletionItem {
            Label = item.Label,
            SortText = item.SortText,
            FilterText = item.FilterText,
            Detail = item.Detail,
            Kind = item.Kind,
            Deprecated = item.Deprecated,
            Documentation = documentation,
            InsertTextMode = InsertTextMode.AsIs,
            AdditionalTextEdits = item.AdditionalTextEdits ?? additionalTextEdits?.ToList(),
            TextEdit = item.TextEdit ?? currentLineTextEdit
        };
    }

    private static (TextEditOrInsertReplaceEdit, IEnumerable<AnnotatedTextEdit>) ArrangeTextEdits(IEnumerable<TextChange> changes, RoslynCompletionItem completionItem, SourceText sourceText) {
        var additionalTextEdits = changes
            .Where(x => !x.Span.IntersectsWith(completionItem.Span))
            .Select(x => x.ToAnnotatedTextEdit(sourceText))
            .ToList();

        var currentLineTextEdit = changes
            .FirstOrDefault(x => x.Span.IntersectsWith(completionItem.Span))
            .ToTextEdit(sourceText);

        // Remove previous text manualy because fucking vscode bydesign
        var completionItemRange = completionItem.Span.ToRange(sourceText);
        additionalTextEdits.Add(new ProtocolModels.TextEdit.AnnotatedTextEdit() {
            NewText = string.Empty,
            Range = new ProtocolModels.DocumentRange() {
                Start = currentLineTextEdit.Range.Start,
                End = completionItemRange.Start
            }
        });
        additionalTextEdits.Add(new ProtocolModels.TextEdit.AnnotatedTextEdit() {
            NewText = string.Empty,
            Range = new ProtocolModels.DocumentRange() {
                Start = completionItemRange.End,
                End = currentLineTextEdit.Range.End,
            }
        });

        return (new TextEditOrInsertReplaceEdit(currentLineTextEdit), additionalTextEdits);
    }
}