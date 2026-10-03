using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class DocumentFormattingHandlerBase : IHandler {
    protected abstract Task<List<TextEdit>?> Handle(DocumentFormattingParams request, CancellationToken token);
    protected abstract Task<List<TextEdit>?> Handle(DocumentRangeFormattingParams request, CancellationToken token);
    protected abstract Task<List<TextEdit>?> Handle(DocumentRangesFormattingParams request, CancellationToken token);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddRequestHandler<DocumentFormattingParams, List<TextEdit>?>("textDocument/formatting", Handle);
        server.AddRequestHandler<DocumentRangeFormattingParams, List<TextEdit>?>("textDocument/rangeFormatting", Handle);
        server.AddRequestHandler<DocumentRangesFormattingParams, List<TextEdit>?>("textDocument/rangesFormatting", Handle);
    }
}
