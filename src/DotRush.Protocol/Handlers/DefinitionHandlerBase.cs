using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class DefinitionHandlerBase : IHandler {
    protected abstract Task<List<Location>?> Handle(DefinitionParams request, CancellationToken cancellationToken);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddRequestHandler<DefinitionParams, List<Location>?>("textDocument/definition", Handle);
    }
}
