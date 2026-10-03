using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class DidChangeConfigurationHandlerBase : IHandler {
    protected abstract Task Handle(DidChangeConfigurationParams request, CancellationToken token);

    public virtual void RegisterCapability(ServerCapabilities serverCapabilities) { }
    public void RegisterHandler(LanguageServer server) {
        server.AddNotificationHandler<DidChangeConfigurationParams>("workspace/didChangeConfiguration", Handle);
    }
}
