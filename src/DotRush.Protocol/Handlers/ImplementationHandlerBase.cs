using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class ImplementationHandlerBase : IHandler {
    protected abstract Task<List<Location>?> Handle(ImplementationParams request, CancellationToken cancellationToken);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddRequestHandler<ImplementationParams, List<Location>?>("textDocument/implementation", Handle);
    }
}
