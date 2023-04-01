using dotRush.Server.Extensions;
using dotRush.Server.Services;
using LanguageServer;
using LanguageServer.Parameters.TextDocument;
using Microsoft.CodeAnalysis;
using CodeAnalysis = Microsoft.CodeAnalysis;

namespace dotRush.Server;

public class ServerSession : Session {
    public ServerSession(Stream input, Stream output) : base(input, output) {}

#region Event: DocumentSync 
    protected override void DidChangeTextDocument(DidChangeTextDocumentParams @params) {
        var documentId = SolutionService.Instance!.CurrentSolution?.GetDocumentIdsWithFilePath(@params.textDocument.uri.AbsolutePath).FirstOrDefault();
        var document = SolutionService.Instance.CurrentSolution?.GetDocument(documentId);
        if (documentId == null || document == null) 
            return;

        var originText = document.GetTextAsync().Result;
        var newText = originText.WithChanges(@params.contentChanges.Select(change => {
            var start = change.range.start.ToOffset(document);
            var end = change.range.end.ToOffset(document);
            return new CodeAnalysis.Text.TextChange(CodeAnalysis.Text.TextSpan.FromBounds(start, end), change.text);
        }));
        
        SolutionService.Instance.UpdateSolution(document.Project.Solution.WithDocumentText(documentId, newText));
        CompilationService.Instance?.Compile(document.FilePath!, Proxy);
    }
#endregion
#region Event: DocumentOpen
    protected override void DidOpenTextDocument(DidOpenTextDocumentParams @params) {
        CompilationService.Instance?.Compile(@params.textDocument.uri.AbsolutePath, Proxy);
    }
#endregion
#region Event: Completion
    protected override Result<CompletionResult, ResponseError> Completion(CompletionParams @params) {
        var documentId = SolutionService.Instance!.CurrentSolution?.GetDocumentIdsWithFilePath(@params.textDocument.uri.AbsolutePath).FirstOrDefault();
        var document = SolutionService.Instance.CurrentSolution?.GetDocument(documentId);
        var completitionService = Microsoft.CodeAnalysis.Completion.CompletionService.GetService(document);
        if (completitionService == null || document == null) 
            return Result<CompletionResult, ResponseError>.Error(new ResponseError() {
                code = ErrorCodes.RequestCancelled,
                message = "Could not get completions",
            });

        var position = @params.position.ToOffset(document);
        CodeAnalysis.Completion.CompletionList? completions = null;
        try { 
            completions = completitionService.GetCompletionsAsync(document, position).Result; 
        } catch {}

        if (completions == null) 
            return Result<CompletionResult, ResponseError>.Error(new ResponseError() {
                code = ErrorCodes.RequestCancelled,
                message = "Could not get completions",
            });

        return Result<CompletionResult, ResponseError>.Success(new CompletionResult(
            completions.ItemsList.Select(item => new CompletionItem() {
                label = item.DisplayText,
                kind = item.Tags.First().ToCompletionKind(),
                sortText = item.SortText,
                filterText = item.FilterText,
                insertTextFormat = InsertTextFormat.PlainText,
            }).ToArray()
        ));
    }
#endregion 
}