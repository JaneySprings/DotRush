using DotRush.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace DotRush.Server.Handlers;

public class DidChangeTextDocumentHandler : DidChangeTextDocumentHandlerBase {
    private readonly WorkspaceService solutionService;
    private readonly CompilationService compilationService;
    private readonly ILanguageServerFacade serverFacade;

    public DidChangeTextDocumentHandler(ILanguageServerFacade serverFacade, WorkspaceService solutionService, CompilationService compilationService) {
        this.compilationService = compilationService;
        this.solutionService = solutionService;
        this.serverFacade = serverFacade;
    }

    protected override TextDocumentChangeRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) {
        return new TextDocumentChangeRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForAllDocuments,
            SyncKind = TextDocumentSyncKind.Full
        };
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        var text = request.ContentChanges.First().Text;
        
        solutionService.UpdateDocument(filePath, text);
        compilationService.Diagnostics.OpenDocument(filePath);
        _ = compilationService.PublishDiagnosticsAsync();
        return Unit.Task;
    }
}