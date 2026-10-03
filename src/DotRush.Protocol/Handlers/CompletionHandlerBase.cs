using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class CompletionHandlerBase : IHandler {
    protected abstract Task<CompletionList?> Handle(CompletionParams request, CancellationToken token);
    protected abstract Task<CompletionItem> Resolve(CompletionItem item, CancellationToken token);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddRequestHandler<CompletionParams, CompletionList?>("textDocument/completion", Handle);
        server.AddRequestHandler<CompletionItem, CompletionItem>("completionItem/resolve", Resolve);
    }
}
