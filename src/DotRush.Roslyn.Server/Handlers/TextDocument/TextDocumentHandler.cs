using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.Options;
using EmmyLua.LanguageServer.Framework.Protocol.Message.TextDocument;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using EmmyLua.LanguageServer.Framework.Server.Handler;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class TextDocumentHandler : TextDocumentHandlerBase {
    private readonly WorkspaceService solutionService;
    private readonly CodeAnalysisService codeAnalysisService;

    public TextDocumentHandler(WorkspaceService solutionService, CodeAnalysisService codeAnalysisService) {
        this.solutionService = solutionService;
        this.codeAnalysisService = codeAnalysisService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.TextDocumentSync = new TextDocumentSyncOptions {
            Change = TextDocumentSyncKind.Full,
            OpenClose = true,
        };
    }

    protected override Task Handle(DidOpenTextDocumentParams request, CancellationToken token) {
        return Task.CompletedTask;
    }
    protected override Task Handle(DidChangeTextDocumentParams request, CancellationToken token) {
        var filePath = request.TextDocument.Uri.FileSystemPath;
        var text = request.ContentChanges.First().Text;

        solutionService.UpdateDocument(filePath, text);
        codeAnalysisService.CancelPendingDiagnostics();
        return Task.CompletedTask;
    }
    protected override Task Handle(DidCloseTextDocumentParams request, CancellationToken token) {
        return Task.CompletedTask;
    }
    protected override Task Handle(WillSaveTextDocumentParams request, CancellationToken token) {
        return Task.CompletedTask;
    }
    protected override Task<List<TextEdit>?> HandleRequest(WillSaveTextDocumentParams request, CancellationToken token) {
        return Task.FromResult<List<TextEdit>?>(null);
    }
}