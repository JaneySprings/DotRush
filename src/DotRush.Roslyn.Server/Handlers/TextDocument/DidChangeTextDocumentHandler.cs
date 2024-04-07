using DotRush.Roslyn.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class DidChangeTextDocumentHandler : DidChangeTextDocumentHandlerBase {
    private readonly WorkspaceService solutionService;
    private readonly DiagnosticService diagnosticService;

    public DidChangeTextDocumentHandler(WorkspaceService solutionService, DiagnosticService diagnosticService) {
        this.diagnosticService = diagnosticService;
        this.solutionService = solutionService;
    }

    protected override TextDocumentChangeRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) {
        return new TextDocumentChangeRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForAllDocuments,
            SyncKind = TextDocumentSyncKind.Full
        };
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        var text = request.ContentChanges.First().Text;

        solutionService.UpdateDocument(filePath, text);
        diagnosticService.OpenDocument(filePath);
        _ = diagnosticService.PublishDiagnosticsAsync();
        return Unit.Task;
    }
}