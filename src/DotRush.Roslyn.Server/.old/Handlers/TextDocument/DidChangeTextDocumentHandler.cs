using DotRush.Roslyn.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class DidChangeTextDocumentHandler : DidChangeTextDocumentHandlerBase {
    private readonly WorkspaceService solutionService;
    private readonly CodeAnalysisService codeAnalysisService;

    public DidChangeTextDocumentHandler(WorkspaceService solutionService, CodeAnalysisService codeAnalysisService) {
        this.codeAnalysisService = codeAnalysisService;
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
        _ = codeAnalysisService.PublishDiagnosticsAsync(filePath);
        return Unit.Task;
    }
}