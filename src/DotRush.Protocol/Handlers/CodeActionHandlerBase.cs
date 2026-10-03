using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class CodeActionHandlerBase : IHandler {
    protected abstract Task<List<CodeAction>> Handle(CodeActionParams request, CancellationToken token);
    protected abstract Task<CodeAction?> Resolve(CodeAction request, CancellationToken token);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddRequestHandler<CodeActionParams, List<CodeAction>>("textDocument/codeAction", Handle);
        server.AddRequestHandler<CodeAction, CodeAction?>("codeAction/resolve", Resolve);
    }
}
