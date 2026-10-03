using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class DocumentSymbolHandlerBase : IHandler {
    protected abstract Task<List<DocumentSymbol>> Handle(DocumentSymbolParams request, CancellationToken token);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddRequestHandler<DocumentSymbolParams, List<DocumentSymbol>>("textDocument/documentSymbol", Handle);
    }
}
