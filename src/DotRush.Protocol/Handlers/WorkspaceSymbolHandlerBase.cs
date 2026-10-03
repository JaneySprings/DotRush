using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class WorkspaceSymbolHandlerBase : IHandler {
    protected abstract Task<List<WorkspaceSymbol>> Handle(WorkspaceSymbolParams request, CancellationToken token);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddRequestHandler<WorkspaceSymbolParams, List<WorkspaceSymbol>>("workspace/symbol", Handle);
    }
}
