using LanguageServer;
using LanguageServer.Parameters;
using LanguageServer.Parameters.TextDocument;
using LanguageServer.Parameters.Workspace;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Completion;
using dotRush.Server.Extensions;
using dotRush.Server.Services;

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
        Microsoft.CodeAnalysis.Completion.CompletionList? completions = null;
        try { 
            completions = completionService.GetCompletionsAsync(document, position).Result; 
        } catch(Exception ex) {
            LoggingService.Instance?.LogError(ex.Message, ex);
        }

        if (completions == null) return Result<CompletionResult, ResponseError>.Error(new ResponseError() {
            code = ErrorCodes.RequestCancelled,
            message = "Could not get completions",
        });

        return Result<CompletionResult, ResponseError>.Success(new CompletionResult(
            completions.ItemsList.Select(item => new LanguageServer.Parameters.TextDocument.CompletionItem() {
                label = item.DisplayText,
                kind = item.Tags.First().ToCompletionKind(),
                sortText = item.SortText,
                filterText = item.FilterText,
                insertTextFormat = InsertTextFormat.PlainText,
            }).ToArray()
        ));
    }
#endregion 
#region Event: Definitions
    protected override Result<LocationSingleOrArray, ResponseError> GotoDefinition(TextDocumentPositionParams @params) {
        var symbol = SemanticConverter.GetSymbolForPosition(@params.position, @params.textDocument.uri.AbsolutePath);
        if (symbol == null) return Result<LocationSingleOrArray, ResponseError>.Error(new ResponseError() {
            code = ErrorCodes.RequestCancelled,
            message = "Could not get definition",
        });

        var definitions = symbol.Locations.Select(loc => loc.ToLocation());
        return Result<LocationSingleOrArray, ResponseError>.Success(new LocationSingleOrArray(definitions.ToArray()));
    }
#endregion
#region Event: Implementations
    protected override Result<LocationSingleOrArray, ResponseError> GotoImplementation(TextDocumentPositionParams @params) {
        var symbol = SemanticConverter.GetSymbolForPosition(@params.position, @params.textDocument.uri.AbsolutePath);
        var solution = SolutionService.Instance?.Solution;
        if (symbol == null || solution == null) 
            return Result<LocationSingleOrArray, ResponseError>.Error(new ResponseError() {
                code = ErrorCodes.RequestCancelled,
                message = "Could not get implementation",
            });

        var impl = SymbolFinder.FindImplementationsAsync(symbol, solution).Result;
        var implementations = impl.SelectMany(i => i.Locations).Select(loc => loc.ToLocation());
        return Result<LocationSingleOrArray, ResponseError>.Success(new LocationSingleOrArray(implementations.ToArray()));
    }
#endregion
#region Event: FindReferences
    protected override Result<LanguageServer.Parameters.Location[], ResponseError> FindReferences(ReferenceParams @params) {
        var symbol = SemanticConverter.GetSymbolForPosition(@params.position, @params.textDocument.uri.AbsolutePath);
        var solution = SolutionService.Instance?.Solution;
        if (symbol == null || solution == null) return Result<LanguageServer.Parameters.Location[], ResponseError>.Error(new ResponseError() {
            code = ErrorCodes.RequestCancelled,
            message = "Could not get references",
        });

        var refs = SymbolFinder.FindReferencesAsync(symbol, solution).Result;
        var references = refs.SelectMany(r => r.Locations).Select(loc => loc.ToLocation());
        return Result<LanguageServer.Parameters.Location[], ResponseError>.Success(references.ToArray());
    }
#endregion
}