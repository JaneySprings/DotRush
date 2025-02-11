using System.Text;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RoslynCompletionItem = Microsoft.CodeAnalysis.Completion.CompletionItem;
using RoslynCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Workspaces.Extensions;

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

    protected override CompletionRegistrationOptions CreateRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities) {
        return new CompletionRegistrationOptions {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments,
            TriggerCharacters = new[] { ".", " ", "(", "<" },
            ResolveProvider = true,
        };
    }

    public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync(new CompletionList(), async () => {
            var documentId = solutionService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.GetFileSystemPath()).FirstOrDefault();
            targetDocument = solutionService.Solution?.GetDocument(documentId);
            roslynCompletionService = RoslynCompletionService.GetService(targetDocument);
            if (roslynCompletionService == null || targetDocument == null)
                return new CompletionList(Enumerable.Empty<CompletionItem>());

            var sourceText = await targetDocument.GetTextAsync(cancellationToken);
            var offset = request.Position.ToOffset(sourceText);
            var completions = (completionOptions == null || configurationService.ShowItemsFromUnimportedNamespaces)
                ? await roslynCompletionService.GetCompletionsAsync(targetDocument, offset, cancellationToken: cancellationToken)
                : await roslynCompletionService.GetCompletionsAsync(targetDocument, offset, completionOptions, cancellationToken);

            if (completions == null)
                return new CompletionList(Enumerable.Empty<CompletionItem>());

            codeAnalysisCompletionItems = completions.ItemsList;
            return new CompletionList(completions.ItemsList.Select(item => new CompletionItem() {
                Label = item.DisplayTextPrefix + item.DisplayText + item.DisplayTextSuffix,
                SortText = item.SortText,
                FilterText = item.FilterText,
                Detail = item.InlineDescription,
                Data = item.GetHashCode(),
                Kind = item.Tags[0].ToCompletionItemKind(),
                Preselect = item.Rules.MatchPriority == Microsoft.CodeAnalysis.Completion.MatchPriority.Preselect,
            }));
        });
    }
    public override async Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken) {
        if (targetDocument == null || request.Data == null || roslynCompletionService == null)
            return request;

        var roslynCompletionItem = codeAnalysisCompletionItems?.FirstOrDefault(x => x.GetHashCode() == request.Data.ToObject<int>());
        if (roslynCompletionItem == null)
            return request;

        IEnumerable<TextEdit>? additionalTextEdits = null;
        TextEdit? currentLineTextEdit = null;
        StringOrMarkupContent? documentation = null;

        if (request.Documentation is null) {
            var description = await roslynCompletionService.GetDescriptionAsync(targetDocument, roslynCompletionItem, cancellationToken);
            if (description != null) {
                var stringBuilder = new StringBuilder();
                MarkdownConverter.TaggedTextToMarkdown(description.TaggedParts, stringBuilder);
                documentation = new StringOrMarkupContent(new MarkupContent() {
                    Kind = MarkupKind.Markdown,
                    Value = stringBuilder.ToString()
                });
            }
        }

        if (request.AdditionalTextEdits is null || request.TextEdit is null) {
            var changes = await roslynCompletionService.GetChangeAsync(targetDocument, roslynCompletionItem, cancellationToken: cancellationToken);
            var sourceText = await targetDocument.GetTextAsync(cancellationToken);
            if (changes?.TextChanges == null) {
                CurrentSessionLogger.Debug($"No text changes found for item:[{request.Label}]");
                return request;
            }
            (currentLineTextEdit, additionalTextEdits) = ArrangeTextEdits(changes.TextChanges, roslynCompletionItem, sourceText);
        }

        return new CompletionItem {
            Label = request.Label,
            SortText = request.SortText,
            FilterText = request.FilterText,
            Detail = request.Detail,
            Kind = request.Kind,
            Preselect = request.Preselect,
            Documentation = documentation,
            InsertTextMode = InsertTextMode.AsIs,
            AdditionalTextEdits = request.AdditionalTextEdits ?? TextEditContainer.From(additionalTextEdits),
            TextEdit = request.TextEdit ?? TextEditOrInsertReplaceEdit.From(currentLineTextEdit!)
        };
    }

    private static (TextEdit, IEnumerable<TextEdit>) ArrangeTextEdits(IEnumerable<TextChange> changes, RoslynCompletionItem completionItem, SourceText sourceText) {
        var additionalTextEdits = changes
            .Where(x => !x.Span.IntersectsWith(completionItem.Span))
            .Select(x => x.ToTextEdit(sourceText))
            .ToList();

        var currentLineTextEdit = changes
            .First(x => x.Span.IntersectsWith(completionItem.Span))
            .ToTextEdit(sourceText);

        // Remove previous text manualy because fucking vscode bydesign
        var completionItemRange = completionItem.Span.ToRange(sourceText);
        additionalTextEdits.Add(new TextEdit() {
            NewText = string.Empty,
            Range = new ProtocolModels.Range() {
                Start = currentLineTextEdit.Range.Start,
                End = completionItemRange.Start
            }
        });
        additionalTextEdits.Add(new TextEdit() {
            NewText = string.Empty,
            Range = new ProtocolModels.Range() {
                Start = completionItemRange.End,
                End = currentLineTextEdit.Range.End,
            }
        });

        return (currentLineTextEdit, additionalTextEdits);
    }
}