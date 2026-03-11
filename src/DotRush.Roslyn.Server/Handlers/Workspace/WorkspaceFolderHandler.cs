using DotRush.Common.Extensions;
using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.WorkspaceServerCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Message.WorkspaceFolders;
using EmmyLua.LanguageServer.Framework.Server.Handler;

namespace DotRush.Roslyn.Server.Handlers.Workspace;

public class WorkspaceFolderHandler : WorkspaceFolderHandlerBase {
    private readonly WorkspaceService workspaceService;
    private readonly ConfigurationService configurationService;

    public WorkspaceFolderHandler(WorkspaceService workspaceService, ConfigurationService configurationService) {
        this.workspaceService = workspaceService;
        this.configurationService = configurationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.Workspace ??= new WorkspaceServerCapabilities();
        serverCapabilities.Workspace.WorkspaceFolders = new WorkspaceFoldersServerCapabilities {
            Supported = true,
            ChangeNotifications = true
        };
    }
    protected override Task Handle(DidChangeWorkspaceFoldersParams request, CancellationToken token) {
        if (!configurationService.ApplyWorkspaceChanges) //TODO: Create a separate property for this
            return Task.CompletedTask;

        var projectFiles = request.Event?.Removed?.SelectMany(it => FileSystemExtensions.GetFirstFiles(it.Uri.FileSystemPath, [".csproj"]));
        if (projectFiles == null)
            return Task.CompletedTask;

        foreach (var projectFile in projectFiles)
            workspaceService?.UnloadProject(projectFile);

        return Task.CompletedTask;
    }
}