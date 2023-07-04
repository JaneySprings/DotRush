using DotRush.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace DotRush.Server.Handlers;

public class DocumentSyncHandler : TextDocumentSyncHandlerBase {
    private readonly SolutionService solutionService;
    private readonly CompilationService compilationService;
    private readonly CodeActionService codeActionService;
    private readonly ILanguageServerFacade serverFacade;
    

    public DocumentSyncHandler(ILanguageServerFacade serverFacade, SolutionService solutionService, CompilationService compilationService, CodeActionService codeActionService) {
        this.solutionService = solutionService;
        this.compilationService = compilationService;
        this.codeActionService = codeActionService;
        this.serverFacade = serverFacade;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(SynchronizationCapability capability, ClientCapabilities clientCapabilities) {
        return new TextDocumentSyncRegistrationOptions {
            DocumentSelector = DocumentSelector.ForLanguage("csharp", "xaml", "xml"),
            Change = TextDocumentSyncKind.Full
        };
    }
    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) {
        return new TextDocumentAttributes(uri, "csharp");
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        var text = request.ContentChanges.First().Text;
        switch (Path.GetExtension(filePath)) {
            case ".cs":
                this.solutionService.UpdateCSharpDocument(filePath, text);
                break;
            default:
                this.solutionService.UpdateAdditionalDocument(filePath, text);
                break;
        }

        this.compilationService.AddDocument(filePath);
        this.compilationService.DiagnoseAsync(filePath, serverFacade.TextDocument);
        this.compilationService.AnalyzerDiagnoseAsync(request.TextDocument.Uri.GetFileSystemPath(), serverFacade.TextDocument);
        return Unit.Task;
    }
    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();

        this.compilationService.AddDocument(filePath);
        this.compilationService.DiagnoseAsync(filePath, serverFacade.TextDocument);
        this.compilationService.AnalyzerDiagnoseAsync(filePath, serverFacade.TextDocument);
        return Unit.Task;
    }
    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();

        this.compilationService.CancelDiagnostics();
        this.compilationService.CancelAnalyzerDiagnostics();
        this.compilationService.RemoveDocument(filePath, serverFacade.TextDocument);
        this.codeActionService.CodeActions.ClearWithFilePath(filePath);
        return Unit.Task;
    }
    public  override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken) {
        return Unit.Task;
    }
}