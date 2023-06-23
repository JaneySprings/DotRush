using DotRush.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace DotRush.Server.Handlers;

public class DocumentSyncHandler : ITextDocumentSyncHandler {
    private readonly SolutionService solutionService;
    private readonly CompilationService compilationService;
    private readonly CodeActionService codeActionService;
    private readonly ILanguageServerFacade serverFacade;
    private CancellationTokenSource? analyzersCancellationTokenSource;

    public DocumentSyncHandler(ILanguageServerFacade serverFacade, SolutionService solutionService, CompilationService compilationService, CodeActionService codeActionService) {
        this.solutionService = solutionService;
        this.compilationService = compilationService;
        this.codeActionService = codeActionService;
        this.serverFacade = serverFacade;
    }

    public TextDocumentChangeRegistrationOptions GetRegistrationOptions(SynchronizationCapability capability, ClientCapabilities clientCapabilities) {
        return new TextDocumentSyncRegistrationOptions {
            DocumentSelector = DocumentSelector.ForLanguage("csharp", "xaml", "xml"),
            Change = TextDocumentSyncKind.Full
        };
    }
    TextDocumentOpenRegistrationOptions IRegistration<TextDocumentOpenRegistrationOptions, SynchronizationCapability>.GetRegistrationOptions(SynchronizationCapability capability, ClientCapabilities clientCapabilities) {
        return new TextDocumentSyncRegistrationOptions {
            DocumentSelector = DocumentSelector.ForLanguage("csharp")
        };
    }
    TextDocumentCloseRegistrationOptions IRegistration<TextDocumentCloseRegistrationOptions, SynchronizationCapability>.GetRegistrationOptions(SynchronizationCapability capability, ClientCapabilities clientCapabilities) {
        return new TextDocumentSyncRegistrationOptions {
            DocumentSelector = DocumentSelector.ForLanguage("csharp")
        };
    }
    TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions, SynchronizationCapability>.GetRegistrationOptions(SynchronizationCapability capability, ClientCapabilities clientCapabilities) {
        return new TextDocumentSyncRegistrationOptions();
    }
    public TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) {
        return new TextDocumentAttributes(uri, "csharp");
    }

    public Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        var text = request.ContentChanges.First().Text;
        switch (Path.GetExtension(filePath)) {
            case ".cs":
                this.solutionService.UpdateCSharpDocument(filePath, text);
                break;
            default:
                this.solutionService.UpdateAdditionalDocument(filePath, text);
                return Unit.Task;
        }
    
        var diagnosticCancellation = GetToken();
        this.compilationService.AddDocument(filePath);
        this.compilationService.DiagnoseAsync(filePath, serverFacade.TextDocument, diagnosticCancellation);
        this.compilationService.AnalyzerDiagnoseAsync(request.TextDocument.Uri.GetFileSystemPath(), serverFacade.TextDocument, diagnosticCancellation);
        return Unit.Task;
    }
    public Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        var diagnosticCancellation = GetToken();

        this.compilationService.AddDocument(filePath);
        this.compilationService.DiagnoseAsync(filePath, serverFacade.TextDocument, diagnosticCancellation);
        this.compilationService.AnalyzerDiagnoseAsync(filePath, serverFacade.TextDocument, diagnosticCancellation);
        return Unit.Task;
    }
    public Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        var diagnosticCancellation = GetToken(); // cancel any diagnostics for this file
    
        this.compilationService.RemoveDocument(filePath, serverFacade.TextDocument);
        this.codeActionService.CodeActions.ClearWithFilePath(filePath);
        return Unit.Task;
    }
    public Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken) {
        return Unit.Task;
    }
    
    private CancellationToken GetToken() {
        this.analyzersCancellationTokenSource?.Cancel();
        this.analyzersCancellationTokenSource?.Dispose();
        this.analyzersCancellationTokenSource = new CancellationTokenSource();
        return this.analyzersCancellationTokenSource.Token;
    }
}