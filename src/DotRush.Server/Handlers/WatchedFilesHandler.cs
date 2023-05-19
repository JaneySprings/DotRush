using DotRush.Server.Services;
using MediatR;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace DotRush.Server.Handlers;

public class WatchedFilesHandler : DidChangeWatchedFilesHandlerBase {
    private readonly SolutionService solutionService;
    private readonly CompilationService compilationService;
    private readonly ILanguageServerFacade serverFacade;

    public WatchedFilesHandler(ILanguageServerFacade serverFacade, SolutionService solutionService, CompilationService compilationService) {
        this.solutionService = solutionService;
        this.compilationService = compilationService;
        this.serverFacade = serverFacade;
    }

    protected override DidChangeWatchedFilesRegistrationOptions CreateRegistrationOptions(DidChangeWatchedFilesCapability capability, ClientCapabilities clientCapabilities) {
        return new DidChangeWatchedFilesRegistrationOptions();
    }

    public override async Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken) {
        if (request.Changes.Any(it => it.Type == FileChangeType.Created)) {
            await this.solutionService.ReloadSolution(cancellationToken);
            return Unit.Value;
        }

        foreach (var change in request.Changes) {
            if (!change.Uri.GetFileSystemPath().EndsWith(".cs")) {
                foreach (var file in Directory.GetFiles(change.Uri.GetFileSystemPath(), "*.cs", SearchOption.AllDirectories)) {
                    if (change.Type == FileChangeType.Deleted)
                        this.DeleteSourceDocument(change.Uri.GetFileSystemPath());
                }
            } else {
                if (change.Type == FileChangeType.Deleted)
                    this.DeleteSourceDocument(change.Uri.GetFileSystemPath());
            }
        }

        return Unit.Value;
    }

    private void DeleteSourceDocument(string path) {
        var documentIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(path);
        if (documentIds is null) 
            return;

        foreach (var documentId in documentIds) {
            var updates = this.solutionService.Solution?.RemoveDocument(documentId);
            if (updates is null) 
                continue;

            this.solutionService.UpdateSolution(updates);
        }
    }
}