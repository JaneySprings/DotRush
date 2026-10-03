using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class RenameHandlerBase : IHandler {
    protected abstract Task<DocumentRange?> Handle(PrepareRenameParams request, CancellationToken token);
    protected abstract Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken token);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddRequestHandler<PrepareRenameParams, DocumentRange?>("textDocument/prepareRename", Handle);
        server.AddRequestHandler<RenameParams, WorkspaceEdit?>("textDocument/rename", Handle);
    }
}
