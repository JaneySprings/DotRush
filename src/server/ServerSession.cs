using LanguageServer;
using LanguageServer.Parameters;
using LanguageServer.Parameters.TextDocument;
using LanguageServer.Parameters.Workspace;
using Microsoft.CodeAnalysis.FindSymbols;
using dotRush.Server.Extensions;
using dotRush.Server.Services;
using dotRush.Server.Handlers;

namespace dotRush.Server;

public class ServerSession : Session {
    public ServerSession(Stream input, Stream output) : base(input, output) {}

#region Event: DocumentSync 
    protected override void DidChangeTextDocument(DidChangeTextDocumentParams @params) {
        DocumentService.Instance.ApplyTextChanges(@params);
        CompilationService.Instance.Compile(@params.textDocument.uri.LocalPath, Proxy);
    }
    protected override void DidOpenTextDocument(DidOpenTextDocumentParams @params) {
        CompilationService.Instance.Compile(@params.textDocument.uri.LocalPath, Proxy);
    }
    protected override void DidChangeWatchedFiles(DidChangeWatchedFilesParams @params) {
        DocumentService.Instance.ApplyChanges(@params);
    }
#endregion
#region Event: FrameworkChanged
    protected override void FrameworkChanged(FrameworkChangedArgs args) {
        SolutionService.Instance.UpdateFramework(args.@params?.framework);
    }
#endregion
#region Event: WorkspaceChanged
    protected override void DidChangeWorkspaceFolders(DidChangeWorkspaceFoldersParams @params) {
        var added = @params.@event.added.Select(folder => folder.uri.LocalPath);
        var removed = @params.@event.removed.Select(folder => folder.uri.LocalPath);
        SolutionService.Instance.RemoveTargets(removed.ToArray());
        SolutionService.Instance.AddTargets(added.ToArray());
    }
#endregion

#region Event: Completion
    protected override Result<CompletionResult, ResponseError> Completion(CompletionParams @params) {
        var result = CompletionService.Instance.GetCompletionItems(@params);
        return Result<CompletionResult, ResponseError>.Success(result);
    }
    protected override Result<CompletionItem, ResponseError> ResolveCompletionItem(CompletionItem @params) {
        CompletionService.Instance.ResolveItemChanges(@params);
        CompletionService.Instance.ResolveItemDocumentation(@params);
        return Result<CompletionItem, ResponseError>.Success(@params);
    }
#endregion 
#region Event: Definitions
    protected override Result<LocationSingleOrArray, ResponseError> GotoDefinition(TextDocumentPositionParams @params) {
        var symbol = SemanticConverter.GetSymbolForPosition(@params.position, @params.textDocument.uri.LocalPath);
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
        var symbol = SemanticConverter.GetSymbolForPosition(@params.position, @params.textDocument.uri.LocalPath);
        var solution = SolutionService.Instance.Solution;
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
        var symbol = SemanticConverter.GetSymbolForPosition(@params.position, @params.textDocument.uri.LocalPath);
        var solution = SolutionService.Instance.Solution;
        if (symbol == null || solution == null) return Result<LanguageServer.Parameters.Location[], ResponseError>.Error(new ResponseError() {
            code = ErrorCodes.RequestCancelled,
            message = "Could not get references",
        });

        var refs = SymbolFinder.FindReferencesAsync(symbol, solution).Result;
        var references = refs
            .SelectMany(r => r.Locations)
            .Where(l => File.Exists(l.Document.FilePath))
            .Select(loc => loc.ToLocation());
        return Result<LanguageServer.Parameters.Location[], ResponseError>.Success(references.ToArray());
    }
#endregion
#region Event: CodeActions
    // protected override Result<CodeActionResult, ResponseError> CodeAction(CodeActionParams @params) {
    //     var document = DocumentService.Instance.GetDocumentByPath(@params.textDocument.uri.LocalPath);
    //     if (document == null) return Result<CodeActionResult, ResponseError>.Error(new ResponseError() {
    //         code = ErrorCodes.RequestCancelled,
    //         message = "Could not get code actions",
    //     });

    //     var actions = RefactoringService.Instance.GetCodeActions(document, @params.range);
    //     return Result<CodeActionResult, ResponseError>.Success(new CodeActionResult(actions.ToArray()));
    // }
#endregion

}