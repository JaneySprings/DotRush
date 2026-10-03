using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class SemanticTokensHandlerBase : IHandler {
    protected abstract Task<SemanticTokens?> Handle(SemanticTokensParams request, CancellationToken token);
    protected abstract Task<SemanticTokens?> Handle(SemanticTokensRangeParams request, CancellationToken token);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddRequestHandler<SemanticTokensParams, SemanticTokens?>("textDocument/semanticTokens/full", Handle);
        server.AddRequestHandler<SemanticTokensRangeParams, SemanticTokens?>("textDocument/semanticTokens/range", Handle);
    }
}
