using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class FoldingRangeHandlerBase : IHandler {
    protected abstract Task<List<FoldingRange>> Handle(FoldingRangeParams request, CancellationToken token);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddRequestHandler<FoldingRangeParams, List<FoldingRange>>("textDocument/foldingRange", Handle);
    }
}
