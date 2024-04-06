using DotRush.Roslyn.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DotRush.Roslyn.Server.Handlers;

public class DidCloseTextDocumentHandler : DidCloseTextDocumentHandlerBase {
    private readonly WorkspaceService solutionService;
    private readonly CompilationService compilationService;
    private readonly ILanguageServerFacade serverFacade;

    public DidCloseTextDocumentHandler(ILanguageServerFacade serverFacade, WorkspaceService solutionService, CompilationService compilationService) {
        this.compilationService = compilationService;
        this.solutionService = solutionService;
        this.serverFacade = serverFacade;
    }

    protected override TextDocumentCloseRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) {
        return new TextDocumentCloseRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        compilationService.Diagnostics.CloseDocument(filePath);
        return Unit.Task;
    }
}