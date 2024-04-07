using DotRush.Roslyn.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class DidCloseTextDocumentHandler : DidCloseTextDocumentHandlerBase {
    private readonly DiagnosticService diagnosticService;

    public DidCloseTextDocumentHandler(DiagnosticService diagnosticService) {
        this.diagnosticService = diagnosticService;
    }

    protected override TextDocumentCloseRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) {
        return new TextDocumentCloseRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        diagnosticService.CloseDocument(filePath);
        return Unit.Task;
    }
}