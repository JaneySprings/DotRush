using System.Text;
using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RoslynCompletionItem = Microsoft.CodeAnalysis.Completion.CompletionItem;
using RoslynCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Handlers;

public class CompletionHandler : CompletionHandlerBase {
    private readonly SolutionService solutionService;
    private IEnumerable<RoslynCompletionItem>? codeAnalysisCompletionItems;
    private RoslynCompletionService? roslynCompletionService;
    private Document? targetDocument;

    public CompletionHandler(SolutionService solutionService) {
        this.solutionService = solutionService;
    }

    protected override CompletionRegistrationOptions CreateRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities) {
        return new CompletionRegistrationOptions {
            TriggerCharacters = new[] { ".", ":", " ", "(", "$", "<" },
            ResolveProvider = true,
        };
    }

    public override async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken) {
        var documentId = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath()).FirstOrDefault();
        this.targetDocument = this.solutionService.Solution?.GetDocument(documentId);
        this.roslynCompletionService = RoslynCompletionService.GetService(targetDocument);
        if (this.roslynCompletionService == null || this.targetDocument == null)
            return new CompletionList(Enumerable.Empty<CompletionItem>());

        var sourceText = await this.targetDocument.GetTextAsync(cancellationToken);
        var offset = request.Position.ToOffset(sourceText);

        var completions = await this.roslynCompletionService.GetCompletionsAsync(this.targetDocument, offset, cancellationToken: cancellationToken);
        if (completions == null)
            return new CompletionList(Enumerable.Empty<CompletionItem>());

        this.codeAnalysisCompletionItems = completions.ItemsList;
        var completionItems = new List<CompletionItem>();

        foreach (var item in completions.ItemsList) {
            completionItems.Add(new CompletionItem() {
                Label = item.DisplayTextPrefix + item.DisplayText + item.DisplayTextSuffix,
                SortText = item.SortText,
                FilterText = item.FilterText,
                Detail = item.InlineDescription,
                Data = item.GetHashCode(),
                Kind = item.Tags[0].ToCompletionItemKind(),
                Preselect = item.Rules.MatchPriority == Microsoft.CodeAnalysis.Completion.MatchPriority.Preselect
            });
        }

        return new CompletionList(completionItems);
    }

    public override async Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken) {
        if (this.targetDocument == null || request.Data == null || this.roslynCompletionService == null)
            return request;

        var roslynCompletionItem = this.codeAnalysisCompletionItems?.FirstOrDefault(x => x.GetHashCode() == request.Data.ToObject<int>());
        if (roslynCompletionItem == null)
            return request;

        StringOrMarkupContent? documentation = null;
        IEnumerable<TextEdit>? additionalTextEdits = null;
        TextEdit? textEdit = null;

        if (request.Documentation == null) {
            var description = await this.roslynCompletionService.GetDescriptionAsync(this.targetDocument, roslynCompletionItem, cancellationToken);
            if (description != null) {
                var stringBuilder = new StringBuilder();
                MarkdownConverter.TaggedTextToMarkdown(description.TaggedParts, stringBuilder);
                documentation = new StringOrMarkupContent(new MarkupContent() {
                    Kind = MarkupKind.Markdown,
                    Value = stringBuilder.ToString()
                });
            }
        }

        if (request.TextEdit == null) {
            var changes = await this.roslynCompletionService.GetChangeAsync(this.targetDocument, roslynCompletionItem, cancellationToken: cancellationToken);
            var sourceText = await this.targetDocument.GetTextAsync(cancellationToken);
            if (changes?.TextChanges != null) {
                if (roslynCompletionItem.IsComplexTextEdit) {
                    textEdit = ArrangeTextEdit(changes.TextChanges, roslynCompletionItem, sourceText);
                    additionalTextEdits = ArrangeAdditionalTextEdits(changes.TextChanges, roslynCompletionItem, sourceText);
                } else {
                    textEdit = changes.TextChanges.FirstOrDefault().ToTextEdit(sourceText);
                }
            }
        }

        return new CompletionItem() {
            Label = request.Label,
            SortText = request.SortText,
            FilterText = request.FilterText,
            Detail = request.Detail,
            Kind = request.Kind,
            Preselect = request.Preselect,
            Documentation = documentation,
            InsertTextMode = InsertTextMode.AsIs,
            AdditionalTextEdits = additionalTextEdits != null ? new TextEditContainer(additionalTextEdits) : null,
            TextEdit = textEdit != null ? new TextEditOrInsertReplaceEdit(textEdit) : null,
        };
    }

    private TextEdit? ArrangeTextEdit(IEnumerable<TextChange> changes, RoslynCompletionItem completionItem, SourceText sourceText) {
        // This textEdit removes the text that was already typed by the user
        return new TextEdit { NewText = string.Empty };
    }

    private IEnumerable<TextEdit> ArrangeAdditionalTextEdits(IEnumerable<TextChange> changes, RoslynCompletionItem completionItem, SourceText sourceText) {
        var additionalTextEdits = changes
            .Where(x => !x.Span.IntersectsWith(completionItem.Span))
            .Select(x => x.ToTextEdit(sourceText))
            .ToList();

        var currentLineTextEdit = changes
            .Single(x => x.Span.IntersectsWith(completionItem.Span))
            .ToTextEdit(sourceText);

        if (currentLineTextEdit != null) {
            additionalTextEdits.Add(new TextEdit() {
                NewText = currentLineTextEdit.NewText,
                Range = new ProtocolModels.Range(
                    currentLineTextEdit.Range.Start,
                    completionItem.Span.Start.ToPosition(sourceText)
                ),
            });
        }

        return additionalTextEdits;
    }
}