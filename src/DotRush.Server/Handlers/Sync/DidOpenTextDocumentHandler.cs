using DotRush.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DotRush.Server.Handlers;

public class DidOpenTextDocumentHandler : DidOpenTextDocumentHandlerBase {
    private readonly WorkspaceService solutionService;
    private readonly CompilationService compilationService;
    private readonly ILanguageServerFacade serverFacade;

    public DidOpenTextDocumentHandler(ILanguageServerFacade serverFacade, WorkspaceService solutionService, CompilationService compilationService) {
        this.compilationService = compilationService;
        this.solutionService = solutionService;
        this.serverFacade = serverFacade;
    }

    protected override TextDocumentOpenRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) {
        return new TextDocumentOpenRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        compilationService.Diagnostics.OpenDocument(filePath);
        _ = compilationService.PublishDiagnosticsAsync(filePath);
        return Unit.Task;
    }
}