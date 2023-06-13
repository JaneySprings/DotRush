using System.Text;
using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using CodeAnalysisCompletionItem = Microsoft.CodeAnalysis.Completion.CompletionItem;
using CodeAnalysisCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Handlers;

public class CompletionHandler : CompletionHandlerBase {
    private readonly Dictionary<int, CodeAnalysisCompletionItem> cachedItems;
    private readonly SolutionService solutionService;
    private CompletionParams? completionParams;

    public CompletionHandler(SolutionService solutionService) {
        this.cachedItems = new Dictionary<int, CodeAnalysisCompletionItem>();
        this.solutionService = solutionService;
    }

    protected override CompletionRegistrationOptions CreateRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities) {
        return new CompletionRegistrationOptions {
            TriggerCharacters = new[] { ".", ":", " ", "(", "$" },
            ResolveProvider = true,
        };
    }

    public override async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken) {
        this.completionParams = request;

        var documentId = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath()).FirstOrDefault();
        var document = this.solutionService.Solution?.GetDocument(documentId);
        var completionService = CodeAnalysisCompletionService.GetService(document);
        if (completionService == null || document == null)
            return new CompletionList(Enumerable.Empty<CompletionItem>());

        var sourceText = await document.GetTextAsync(cancellationToken);
        var offset = request.Position.ToOffset(sourceText);

        var completions = await completionService.GetCompletionsAsync(document, offset, cancellationToken: cancellationToken);
        if (completions == null)
            return new CompletionList(Enumerable.Empty<CompletionItem>());
        
        this.cachedItems.Clear();
        return new CompletionList(completions.ItemsList.Select(x => {
            var item = x.ToCompletionItem();
            CacheCompletionItem(item, x);
            return item;
        }));
    }

    public override async Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken) {
        var documentId = this.solutionService.Solution?.GetDocumentIdsWithFilePath(this.completionParams?.TextDocument.Uri.GetFileSystemPath()).FirstOrDefault();
        var document = this.solutionService.Solution?.GetDocument(documentId);
        if (document == null || request.Data == null) 
            return request;

        StringOrMarkupContent? documentation = null;
        IEnumerable<TextEdit>? additionalTextEdits = null;
        TextEdit? textEdit = null;

        var id = request.Data.ToObject<int>();
        var completionService = CodeAnalysisCompletionService.GetService(document);

        if (request.Documentation == null) {
            if (cachedItems.TryGetValue(id, out var item) && completionService != null) {
                var description = await completionService.GetDescriptionAsync(document, item, cancellationToken);
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
                var changes = await completionService.GetChangeAsync(document, item, cancellationToken: cancellationToken);
                var sourceText = await document.GetTextAsync(cancellationToken);

                if (changes != null && item.IsComplexTextEdit) 
                    (textEdit, additionalTextEdits) = ArrangeTextEdits(changes.TextChanges, sourceText);
                else if (changes != null && !item.IsComplexTextEdit) 
                    textEdit = changes?.TextChanges.FirstOrDefault().ToTextEdit(sourceText);
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


    private void CacheCompletionItem(CompletionItem completionItem, CodeAnalysisCompletionItem codeAnalysisCompletionItem) {
        if (completionItem.Data == null)
            return;
        
        var id = completionItem.Data.ToObject<int>();
        this.cachedItems.TryAdd(id, codeAnalysisCompletionItem);
    }
    private (TextEdit?, IEnumerable<TextEdit>?) ArrangeTextEdits(IEnumerable<TextChange> changes, SourceText sourceText) {
        var position = this.completionParams?.Position;
        var additionalTextEdits = changes
            .Where(x => x.Span.Start.ToPosition(sourceText).Line > position?.Line || x.Span.End.ToPosition(sourceText).Line < position?.Line)
            .Select(x => x.ToTextEdit(sourceText))
            .ToList();

        var specificTextEdit = changes
            .FirstOrDefault(x => x.Span.Start.ToPosition(sourceText).Line <= position?.Line && x.Span.End.ToPosition(sourceText).Line >= position?.Line)
            .ToTextEdit(sourceText);

        if (specificTextEdit == null || position == null)
            return (null, additionalTextEdits);

        var wordDelemiters = new char[] { '.', '(', ';', ' ', '{'};
        var line = sourceText.Lines[position.Line].ToString().Substring(0, position.Character);
        var cutPosition = line.LastIndexOfAny(wordDelemiters) + 1;
        if (cutPosition < 1)
            return (specificTextEdit, additionalTextEdits);
        
        additionalTextEdits.Add(new TextEdit() {
            NewText = specificTextEdit.NewText,
            Range = new ProtocolModels.Range(specificTextEdit.Range.Start, new Position {
                Line = position.Line,
                Character = cutPosition,
            }),
        });

        var textEdit = new TextEdit { NewText = string.Empty };
        return (textEdit, additionalTextEdits);
    }
}