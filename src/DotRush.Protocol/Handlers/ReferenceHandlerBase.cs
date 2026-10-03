using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class ReferenceHandlerBase : IHandler {
    protected abstract Task<List<Location>?> Handle(ReferenceParams request, CancellationToken cancellationToken);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddRequestHandler<ReferenceParams, List<Location>?>("textDocument/references", Handle);
    }
}
