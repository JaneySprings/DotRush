using dotRush.Server.Extensions;
using dotRush.Server.Services;
using LanguageServer;
using LanguageServer.Parameters.TextDocument;
using LanguageServer.Parameters.Workspace;
using Microsoft.CodeAnalysis;
using CodeAnalysis = Microsoft.CodeAnalysis;
using CompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace dotRush.Server;

public class ServerSession : Session {
    public ServerSession(Stream input, Stream output) : base(input, output) {}

#region Event: DocumentSync 
    protected override void DidChangeTextDocument(DidChangeTextDocumentParams @params) {
        DocumentService.Instance?.ApplyTextChanges(@params);
        CompilationService.Instance?.Compile(@params.textDocument.uri.AbsolutePath, Proxy);
    }
    protected override void DidOpenTextDocument(DidOpenTextDocumentParams @params) {
        CompilationService.Instance?.Compile(@params.textDocument.uri.AbsolutePath, Proxy);
    }
    protected override void DidChangeWatchedFiles(DidChangeWatchedFilesParams @params) {
        DocumentService.Instance?.ApplyChanges(@params);
    }
#endregion
#region Event: Completion
    protected override Result<CompletionResult, ResponseError> Completion(CompletionParams @params) {
        var document = DocumentService.Instance?.GetDocumentByPath(@params.textDocument.uri.AbsolutePath);
        var completionService = CompletionService.GetService(document);
        if (completionService == null || document == null) 
            return Result<CompletionResult, ResponseError>.Error(new ResponseError() {
                code = ErrorCodes.RequestCancelled,
                message = "Could not get completions",
            });

        var position = @params.position.ToOffset(document);
        CodeAnalysis.Completion.CompletionList? completions = null;
        try { 
            completions = completionService.GetCompletionsAsync(document, position).Result; 
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