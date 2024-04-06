using DotRush.Roslyn.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers;

public class DidOpenTextDocumentHandler : DidOpenTextDocumentHandlerBase {
    private readonly CompilationService compilationService;

    public DidOpenTextDocumentHandler(CompilationService compilationService) {
        this.compilationService = compilationService;
    }

    protected override TextDocumentOpenRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) {
        return new TextDocumentOpenRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        compilationService.Diagnostics.OpenDocument(filePath);
        _ = compilationService.PublishDiagnosticsAsync();
        return Unit.Task;
    }
}