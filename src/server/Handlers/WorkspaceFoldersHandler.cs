using DotRush.Server.Services;
using MediatR;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace DotRush.Server.Handlers;

public class WorkspaceFoldersHandler : DidChangeWorkspaceFoldersHandlerBase {
    private SolutionService solutionService;
    private readonly ILanguageServerFacade serverFacade;

    public WorkspaceFoldersHandler(ILanguageServerFacade serverFacade, SolutionService solutionService) {
        this.serverFacade = serverFacade;
        this.solutionService = solutionService;
    }

    protected override DidChangeWorkspaceFolderRegistrationOptions CreateRegistrationOptions(ClientCapabilities clientCapabilities){
        return new DidChangeWorkspaceFolderRegistrationOptions() {
            ChangeNotifications = true,
            Supported = true,
        };
    }

    public override async Task<Unit> Handle(DidChangeWorkspaceFoldersParams request, CancellationToken cancellationToken) {
        var added = request.Event.Added.Select(folder => folder.Uri.GetFileSystemPath());
        var removed = request.Event.Removed.Select(folder => folder.Uri.GetFileSystemPath());
        var observer = await LanguageServer.CreateWorkDoneObserverAsync();

        if (removed.Any()) {
            this.solutionService.RemoveWorkspaceFolders(removed);
            this.solutionService.ReloadSolutionAsync(observer);
            return Unit.Value;
        }

        if (added.Any()) {
            this.solutionService.AddWorkspaceFolders(added);
            this.solutionService.LoadSolutionAsync(observer);
        }
        
        return Unit.Value;
    }
}