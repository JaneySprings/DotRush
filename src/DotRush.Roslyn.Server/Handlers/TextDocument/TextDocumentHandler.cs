using DotRush.Protocol.Handlers;
using DotRush.Protocol.Models;
using DotRush.Roslyn.Server.Services;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class TextDocumentHandler : TextDocumentHandlerBase {
    private readonly WorkspaceService workspaceService;
    private readonly CodeAnalysisService codeAnalysisService;

    public TextDocumentHandler(WorkspaceService workspaceService, CodeAnalysisService codeAnalysisService) {
        this.workspaceService = workspaceService;
        this.codeAnalysisService = codeAnalysisService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities) {
        serverCapabilities.TextDocumentSync = new TextDocumentSyncOptions {
            Change = TextDocumentSyncKind.Full,
            OpenClose = true,
        };
    }

    protected override Task Handle(DidOpenTextDocumentParams request, CancellationToken token) {
        var filePath = request.TextDocument.Uri.FileSystemPath;
        workspaceService.UpdateDocument(filePath);
        codeAnalysisService.RequestDiagnosticsPublishing(filePath, workspaceService);
        return Task.CompletedTask;
    }
    protected override Task Handle(DidChangeTextDocumentParams request, CancellationToken token) {
        var filePath = request.TextDocument.Uri.FileSystemPath;
        var text = request.ContentChanges.First().Text;

        workspaceService.UpdateDocument(filePath, text);
        codeAnalysisService.RequestDiagnosticsPublishing(filePath, workspaceService);
        return Task.CompletedTask;
    }
    protected override Task Handle(DidCloseTextDocumentParams request, CancellationToken token) {
        return Task.CompletedTask;
    }
}