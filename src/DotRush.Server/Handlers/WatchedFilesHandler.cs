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

    public override Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken) {
        if (request.Changes.Any(it => it.Uri.GetFileSystemPath().Split(Path.DirectorySeparatorChar).Any(it => it.StartsWith("."))))
            return Unit.Task;

        if (request.Changes.Any(it => it.Type == FileChangeType.Created)) {
            this.solutionService.ReloadSolution();
            return Unit.Task;
        }

        return Unit.Task;
    }
}