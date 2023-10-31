using DotRush.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace DotRush.Server.Handlers;

public class DocumentSyncHandler : TextDocumentSyncHandlerBase {
    private readonly WorkspaceService solutionService;
    private readonly CompilationService compilationService;
    private readonly ILanguageServerFacade serverFacade;

    public DocumentSyncHandler(ILanguageServerFacade serverFacade, WorkspaceService solutionService, CompilationService compilationService) {
        this.compilationService = compilationService;
        this.solutionService = solutionService;
        this.serverFacade = serverFacade;
    }


    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) {
        return new TextDocumentSyncRegistrationOptions {
            DocumentSelector = TextDocumentSelector.ForLanguage("csharp", "xaml", "xml"),
            Change = TextDocumentSyncKind.Full
        };
    }
    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) {
        return new TextDocumentAttributes(uri, "csharp");
    }


    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        var text = request.ContentChanges.First().Text;
        if (Path.GetExtension(filePath) != ".cs") {
            solutionService.UpdateAdditionalDocument(filePath, text);
            return Unit.Task;
        }

        solutionService.UpdateCSharpDocument(filePath, text);

        compilationService.ResetCancellationToken();
        compilationService.EnsureDocumentOpened(filePath);
        _ = compilationService.PushTotalDiagnosticsAsync(filePath, request.TextDocument.Version, serverFacade, compilationService.CompilationTokenSource.Token);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        if (Path.GetExtension(filePath) != ".cs")
            return Unit.Task;

        compilationService.ResetCancellationToken();
        compilationService.EnsureDocumentOpened(filePath);
        _ = compilationService.PushTotalDiagnosticsAsync(filePath, request.TextDocument.Version, serverFacade, compilationService.CompilationTokenSource.Token);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken) {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        if (Path.GetExtension(filePath) != ".cs")
            return Unit.Task;
    
        compilationService.Diagnostics.Remove(filePath);
        serverFacade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams() {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>()
        });

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken) {
        return Unit.Task;
    }
}