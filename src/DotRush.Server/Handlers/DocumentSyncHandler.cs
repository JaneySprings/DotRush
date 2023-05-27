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
    private readonly ILanguageServerFacade serverFacade;

    public DocumentSyncHandler(ILanguageServerFacade serverFacade, SolutionService solutionService, CompilationService compilationService) {
        this.solutionService = solutionService;
        this.compilationService = compilationService;
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

    public override async Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken) {
        var documentsIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
        if (documentsIds == null) 
            return Unit.Value;

        foreach (var documentId in documentsIds) {
            var document = this.solutionService.Solution?.GetDocument(documentId);
            if (document == null) {
                var additional = this.solutionService.Solution?.GetAdditionalDocument(documentId);
                HandleAdditionalDocumentChanged(additional, request.ContentChanges.First().Text);
                continue;
            }

            var updatedDocument = document.WithText(SourceText.From(request.ContentChanges.First().Text));
            this.solutionService.UpdateSolution(updatedDocument.Project.Solution);
        }


        await this.compilationService.DiagnoseDocument(request.TextDocument.Uri.GetFileSystemPath(), serverFacade.TextDocument, cancellationToken);
        this.compilationService.AnalyzerDiagnose(request.TextDocument.Uri.GetFileSystemPath(), serverFacade.TextDocument);
        
        return Unit.Value;
    }
    public override async Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken) {
        await this.compilationService.DiagnoseDocument(request.TextDocument.Uri.GetFileSystemPath(), serverFacade.TextDocument, cancellationToken);
        this.compilationService.AnalyzerDiagnose(request.TextDocument.Uri.GetFileSystemPath(), serverFacade.TextDocument);
        return Unit.Value;
    }
    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken) {
        return Unit.Task;
    }
    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken) {
        return Unit.Task;
    }

    private void HandleAdditionalDocumentChanged(CodeAnalysis.TextDocument? textDocument, string newText) {
        if (textDocument == null) 
            return;

        var updates = this.solutionService.Solution?.WithAdditionalDocumentText(textDocument.Id, SourceText.From(newText));
        if (updates == null)
            return;

        this.solutionService.UpdateSolution(updates);
    }
}