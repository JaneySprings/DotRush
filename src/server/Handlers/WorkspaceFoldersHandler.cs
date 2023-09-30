using DotRush.Server.Services;
using MediatR;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace DotRush.Server.Handlers;

public class WorkspaceFoldersHandler : DidChangeWorkspaceFoldersHandlerBase {
    private WorkspaceService workspaceService;
    private readonly ILanguageServerFacade serverFacade;

    public WorkspaceFoldersHandler(ILanguageServerFacade serverFacade, WorkspaceService workspaceService) {
        this.serverFacade = serverFacade;
        this.workspaceService = workspaceService;
    }

    protected override DidChangeWorkspaceFolderRegistrationOptions CreateRegistrationOptions(ClientCapabilities clientCapabilities){
        return new DidChangeWorkspaceFolderRegistrationOptions() {
            ChangeNotifications = true,
            Supported = true,
        };
    }

    public override Task<Unit> Handle(DidChangeWorkspaceFoldersParams request, CancellationToken cancellationToken) {
        var added = request.Event.Added.Select(folder => folder.Uri.GetFileSystemPath());
        var removed = request.Event.Removed.Select(folder => folder.Uri.GetFileSystemPath());

        if (removed.Any()) {
            this.workspaceService.RemoveWorkspaceFolders(removed);
            this.workspaceService.StartSolutionReloading();
            return Unit.Task;
        }

        if (added.Any()) {
            this.workspaceService.AddWorkspaceFolders(added);
            this.workspaceService.StartSolutionLoading();
        }
        
        return Unit.Task;
    }
}