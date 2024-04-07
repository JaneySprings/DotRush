using DotRush.Roslyn.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class DidOpenTextDocumentHandler : DidOpenTextDocumentHandlerBase {
    private readonly DiagnosticService diagnosticService;

    public DidOpenTextDocumentHandler(DiagnosticService diagnosticService) {
        this.diagnosticService = diagnosticService;
    }

    protected override TextDocumentOpenRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) {
        return new TextDocumentOpenRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        diagnosticService.OpenDocument(filePath);
        _ = diagnosticService.PublishDiagnosticsAsync();
        return Unit.Task;
    }
}