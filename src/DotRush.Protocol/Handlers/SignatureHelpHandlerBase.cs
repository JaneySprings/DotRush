using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class SignatureHelpHandlerBase : IHandler {
    protected abstract Task<SignatureHelp> Handle(SignatureHelpParams request, CancellationToken token);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddRequestHandler<SignatureHelpParams, SignatureHelp>("textDocument/signatureHelp", Handle);
    }
}
