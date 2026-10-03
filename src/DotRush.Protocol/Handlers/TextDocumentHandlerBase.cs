using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class TextDocumentHandlerBase : IHandler {
    protected abstract Task Handle(DidOpenTextDocumentParams request, CancellationToken token);
    protected abstract Task Handle(DidChangeTextDocumentParams request, CancellationToken token);
    protected abstract Task Handle(DidCloseTextDocumentParams request, CancellationToken token);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddNotificationHandler<DidOpenTextDocumentParams>("textDocument/didOpen", Handle);
        server.AddNotificationHandler<DidChangeTextDocumentParams>("textDocument/didChange", Handle);
        server.AddNotificationHandler<DidCloseTextDocumentParams>("textDocument/didClose", Handle);
    }
}
