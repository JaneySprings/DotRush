using DotRush.Server.Services;
using MediatR;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using CodeAnalysis = Microsoft.CodeAnalysis;

namespace DotRush.Server.Handlers;

public class DocumentSyncHandler : TextDocumentSyncHandlerBase {
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

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) {
        return new TextDocumentAttributes(uri, "csharp");
    }
    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(SynchronizationCapability capability, ClientCapabilities clientCapabilities) {
        return new TextDocumentSyncRegistrationOptions {
            DocumentSelector = DocumentSelector.ForLanguage("csharp", "xml", "xaml"),
            Change = TextDocumentSyncKind.Full
        };
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        switch (Path.GetExtension(filePath)) {
            case ".cs":
                HandleCSharpDocumentChanges(filePath, request.ContentChanges.First().Text);
                break;
            case ".xaml":
                HandleAdditionalDocumentChanges(filePath, request.ContentChanges.First().Text);
                break;
        }
        
        this.compilationService.DiagnoseAsync(request.TextDocument.Uri.GetFileSystemPath(), serverFacade.TextDocument, GetToken());
        return Unit.Task;
    }
    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken) {
        var compilationToken = GetToken();
        this.compilationService.DeepDiagnoseAsync(request.TextDocument.Uri.GetFileSystemPath(), serverFacade.TextDocument, compilationToken);
        this.compilationService.DiagnoseAsync(request.TextDocument.Uri.GetFileSystemPath(), serverFacade.TextDocument, compilationToken);
        return Unit.Task;
    }
    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken) {
        this.compilationService.ClearAnalyzersDiagnostics(request.TextDocument.Uri.GetFileSystemPath(), serverFacade.TextDocument);
        this.codeActionService.CodeActions.ClearWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
        return Unit.Task;
    }
    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken) {
        return Unit.Task;
    }
    

    private void HandleCSharpDocumentChanges(string documentPath, string newText) {
        var documentIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(documentPath);
        if (documentIds == null)
            return;

        foreach (var documentId in documentIds) {
            var document = this.solutionService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var updatedDocument = document.WithText(SourceText.From(newText));
            this.solutionService.UpdateSolution(updatedDocument.Project.Solution);
        }
    }
    private void HandleAdditionalDocumentChanges(string documentPath, string newText) {
        var documentIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(documentPath);
        if (documentIds == null)
            return;

        foreach (var documentId in documentIds) {
            var updates = this.solutionService.Solution?.WithAdditionalDocumentText(documentId, SourceText.From(newText));
            if (updates == null)
                return;

            this.solutionService.UpdateSolution(updates);
        }
    }

    private CancellationToken GetToken() {
        this.analyzersCancellationTokenSource?.Cancel();
        this.analyzersCancellationTokenSource?.Dispose();
        this.analyzersCancellationTokenSource = new CancellationTokenSource();
        return this.analyzersCancellationTokenSource.Token;
    }
}