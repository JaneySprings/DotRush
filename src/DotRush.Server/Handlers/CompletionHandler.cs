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

namespace DotRush.Server.Handlers;

public class CompletionHandler : CompletionHandlerBase {
    private readonly Dictionary<int, CodeAnalysisCompletionItem> cachedItems;
    private readonly SolutionService solutionService;
    private Document? targetDocument;
    private Position? position;

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
        var completionItems = new List<CompletionItem>();
        var documentId = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath()).FirstOrDefault();
        var document = this.solutionService.Solution?.GetDocument(documentId);
        var completionService = CodeAnalysisCompletionService.GetService(document);
        if (completionService == null || document == null) 
            return new CompletionList(completionItems);

        var sourceText = await document.GetTextAsync(cancellationToken);
        var offset = request.Position.ToOffset(sourceText);
        position = request.Position;

        try {
            var completions = await completionService.GetCompletionsAsync(document, offset, cancellationToken: cancellationToken);
            if (completions == null)
                return new CompletionList(completionItems);
            
            AssignCacheWithDocument(document);
            return new CompletionList(completions.ItemsList.Select(x => {
                var item = x.ToCompletionItem();
                CacheCompletionItem(item, x);
                return item;
            }));
        } catch (Exception e) {
            LoggingService.Instance.LogError(e.Message, e);
            return new CompletionList(completionItems);
        }
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
                var sourceText = await this.targetDocument.GetTextAsync(cancellationToken);

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
    private (TextEdit?, IEnumerable<TextEdit>?) ArrangeTextEdits(IEnumerable<TextChange> changes, SourceText sourceText) {
        var additionalTextEdits = changes
            .Where(x => x.Span.Start.ToPosition(sourceText).Line > position?.Line || x.Span.End.ToPosition(sourceText).Line < position?.Line)
            .Select(x => x.ToTextEdit(sourceText))
            .ToList();

        // TextEdit? textEdit = null;
        var specificTextEdit = changes
            .FirstOrDefault(x => x.Span.Start.ToPosition(sourceText).Line <= position?.Line && x.Span.End.ToPosition(sourceText).Line >= position?.Line)
            .ToTextEdit(sourceText);

        // var cutPosition = position?.Character - specificTextEdit.Range.Start.Character ?? 0;
        // if (cutPosition < 0)
        //     return (specificTextEdit, additionalTextEdits);

        // if (specificTextEdit.Range.Start.Character < position?.Character) {
        //     var leftTextEdit = new TextEdit() {
        //         Range = new ProtocolModels.Range(specificTextEdit.Range.Start, position!),
        //         NewText = string.Empty,
        //     };

        //     additionalTextEdits.Add(leftTextEdit);
        // }
        

        // textEdit = new TextEdit() {
        //     Range = new ProtocolModels.Range(position!, specificTextEdit.Range.End),
        //     NewText = specificTextEdit.NewText.ToStrangeString()
        // };
        

        return (specificTextEdit, additionalTextEdits);
    }

    // private (TextEdit?, IEnumerable<TextEdit>?) ArrangeTextEdits(TextChange change, SourceText sourceText) {
    //     var edit = change.ToTextEdit(sourceText);
    //     var additionalTextEdits = new List<TextEdit>();

    //     additionalTextEdits.Add(new TextEdit {
    //         Range = edit.Range,
    //         NewText = string.Empty,
    //     });

    //     var textEdit = new TextEdit {
    //         NewText = change.NewText,
    //     };
    //     return (textEdit, additionalTextEdits);
    // }
}