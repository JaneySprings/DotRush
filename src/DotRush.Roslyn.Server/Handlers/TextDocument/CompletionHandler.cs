using System.Text;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using RoslynCompletionItem = Microsoft.CodeAnalysis.Completion.CompletionItem;
using RoslynCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Model;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Completion;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.Options;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Markup;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Kind;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Union;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class CompletionHandler : CompletionHandlerBase {
    private readonly WorkspaceService solutionService;
    private readonly ConfigurationService configurationService;
    private IEnumerable<RoslynCompletionItem>? codeAnalysisCompletionItems;
    private RoslynCompletionService? roslynCompletionService;
    private Document? targetDocument;
    private readonly object? completionOptions;

    public CompletionHandler(WorkspaceService solutionService, ConfigurationService configurationService) {
        this.solutionService = solutionService;
        this.configurationService = configurationService;
        completionOptions = CompletionServiceExtensions.GetCompletionOptions();
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
            targetDocument = solutionService.Solution?.GetDocument(documentId);
            roslynCompletionService = RoslynCompletionService.GetService(targetDocument);
            if (roslynCompletionService == null || targetDocument == null)
                return null;

            var sourceText = await targetDocument.GetTextAsync(token).ConfigureAwait(false);
            var offset = request.Position.ToOffset(sourceText);
            var completions = (completionOptions == null || configurationService.ShowItemsFromUnimportedNamespaces)
                ? await roslynCompletionService.GetCompletionsAsync(targetDocument, offset, cancellationToken: token).ConfigureAwait(false)
                : await roslynCompletionService.GetCompletionsAsync(targetDocument, offset, completionOptions, token).ConfigureAwait(false);

            if (completions == null)
                return null;

            codeAnalysisCompletionItems = completions.ItemsList;
            return new CompletionResponse(completions.ItemsList.Select(item => new CompletionItem() {
                Label = item.DisplayTextPrefix + item.DisplayText + item.DisplayTextSuffix,
                SortText = item.SortText,
                FilterText = item.FilterText,
                Detail = item.InlineDescription,
                Data = item.GetHashCode(),
                Kind = item.Tags[0].ToCompletionItemKind(),
                Preselect = item.Rules.MatchPriority == Microsoft.CodeAnalysis.Completion.MatchPriority.Preselect,
            }).ToList());
        });
    }
    protected override async Task<CompletionItem> Resolve(CompletionItem item, CancellationToken token) {
        if (targetDocument == null || item.Data?.Value == null || roslynCompletionService == null)
            return item;

        var roslynCompletionItem = codeAnalysisCompletionItems?.FirstOrDefault(x => x.GetHashCode() == (int)item.Data.Value);
        if (roslynCompletionItem == null)
            return item;

        IEnumerable<AnnotatedTextEdit>? additionalTextEdits = null;
        TextEditOrInsertReplaceEdit? currentLineTextEdit = null;
        var documentation = new MarkupContent();

        if (item.Documentation is null) {
            var description = await roslynCompletionService.GetDescriptionAsync(targetDocument, roslynCompletionItem, token);
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
            var changes = await roslynCompletionService.GetChangeAsync(targetDocument, roslynCompletionItem, cancellationToken: token);
            var sourceText = await targetDocument.GetTextAsync(token);
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
            Preselect = item.Preselect,
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
            .First(x => x.Span.IntersectsWith(completionItem.Span))
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