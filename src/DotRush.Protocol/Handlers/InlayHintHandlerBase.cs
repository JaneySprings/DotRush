using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class InlayHintHandlerBase : IHandler {
    protected abstract Task<List<InlayHint>?> Handle(InlayHintParams request, CancellationToken cancellationToken);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddRequestHandler<InlayHintParams, List<InlayHint>?>("textDocument/inlayHint", Handle);
    }
}
