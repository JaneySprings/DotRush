using DotRush.Server.Services;
using MediatR;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace DotRush.Server.Handlers;

public class WorkspaceFoldersHandler : DidChangeWorkspaceFoldersHandlerBase {
    private WorkspaceService workspaceService;

    public WorkspaceFoldersHandler(WorkspaceService workspaceService) {
        this.workspaceService = workspaceService;
    }

    protected override DidChangeWorkspaceFolderRegistrationOptions CreateRegistrationOptions(ClientCapabilities clientCapabilities) {
        return new DidChangeWorkspaceFolderRegistrationOptions() {
            ChangeNotifications = true,
            Supported = true,
        };
    }

    public override Task<Unit> Handle(DidChangeWorkspaceFoldersParams request, CancellationToken cancellationToken) {
        var added = request.Event.Added.Select(folder => folder.Uri.GetFileSystemPath());
        if (!added.Any())
            return Unit.Task;
        
        workspaceService.WaitHandle.WaitOne();
        workspaceService.AddWorkspaceFolders(added);
        workspaceService.StartSolutionLoading();
        // Automatically skip loaded projects
        
        return Unit.Task;
    }
}