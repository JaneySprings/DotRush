using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.WorkspaceServerCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Message.WorkspaceWatchedFile;
using EmmyLua.LanguageServer.Framework.Protocol.Message.WorkspaceWatchedFile.Watch;
using EmmyLua.LanguageServer.Framework.Server.Handler;

namespace DotRush.Roslyn.Server.Handlers.Workspace;

[Obsolete("Not working correctly for folders", true)]
public class DidChangeWatchedFilesHandler : DidChangeWatchedFilesHandlerBase {
    private readonly WorkspaceService workspaceService;
    private bool shouldApplyWorkspaceChanges;

    public DidChangeWatchedFilesHandler(WorkspaceService workspaceService) {
        this.workspaceService = workspaceService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.Workspace ??= new WorkspaceServerCapabilities();
    }
    protected override Task Handle(DidChangeWatchedFilesParams request, CancellationToken token) {
        // if (workspaceService.Solution == null)
        //     return Task.CompletedTask;

        // shouldApplyWorkspaceChanges = false;
        // foreach (var change in request.Changes) {
        //     var path = change.Uri.FileSystemPath;
        //     HandleFileChange(path, change.Type);

        //     if (change.Type == FileChangeType.Created && Directory.Exists(path)) {
        //         foreach (var filePath in FileSystemExtensions.GetVisibleFilesRecursive(path))
        //             HandleFileChange(filePath, FileChangeType.Created);
        //     }
        // }
        
        // if (shouldApplyWorkspaceChanges)
        //     workspaceService.ApplyChanges();

        return Task.CompletedTask;
    }


    private void HandleFileChange(string path, FileChangeType changeType) {
        if (LanguageExtensions.IsProjectFile(path) && changeType == FileChangeType.Changed)
            return; // Handled from IDE

        if (changeType == FileChangeType.Deleted) {
            workspaceService.DeleteDocument(path);
            // workspaceService.DeleteFolder(path);
            shouldApplyWorkspaceChanges = true;
            return;
        }

        if (changeType == FileChangeType.Changed) {
            workspaceService.UpdateDocument(path);
            return;
        }

        if (changeType == FileChangeType.Created && File.Exists(path)) {
            workspaceService.CreateDocument(path);
            shouldApplyWorkspaceChanges = true;
            return;
        }
    }
}