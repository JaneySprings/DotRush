using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class HoverHandlerBase : IHandler {
    protected abstract Task<Hover?> Handle(HoverParams request, CancellationToken token);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddRequestHandler<HoverParams, Hover?>("textDocument/hover", Handle);
    }
}
