using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace DotRush.Roslyn.Server.Handlers.Workspace;

public class DidChangeWatchedFilesHandler : DidChangeWatchedFilesHandlerBase {
    private readonly WorkspaceService workspaceService;
    private readonly CommandsService commandsService;

    public DidChangeWatchedFilesHandler(WorkspaceService workspaceService, CommandsService commandsService) {
        this.workspaceService = workspaceService;
        this.commandsService = commandsService;
    }

    protected override DidChangeWatchedFilesRegistrationOptions CreateRegistrationOptions(DidChangeWatchedFilesCapability capability, ClientCapabilities clientCapabilities) {
        return new DidChangeWatchedFilesRegistrationOptions() {
            Watchers = new[] {
                new OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher() {
                    Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete,
                    GlobPattern = new GlobPattern("**/*")
                },
            }
        };
    }

    public override Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken) {
        foreach (var change in request.Changes) {
            var path = change.Uri.GetFileSystemPath();
            HandleFileChange(path, change.Type);

            if (change.Type == FileChangeType.Created && Directory.Exists(path)) {
                foreach (var filePath in FileSystemExtensions.GetVisibleFilesRecursive(path))
                    HandleFileChange(filePath, FileChangeType.Created);
            }
        }

        return Unit.Task;
    }

    private void HandleFileChange(string path, FileChangeType changeType) {
        if (LanguageExtensions.IsProjectFile(path) && changeType == FileChangeType.Changed)
            return; // Handled from IDE

        if (changeType == FileChangeType.Deleted) {
            if (IsInternalCommandFile(path))
                commandsService.ResolveCancellation();

            workspaceService.DeleteDocument(path);
            workspaceService.DeleteFolder(path);
            return;
        }

        if (changeType == FileChangeType.Changed) {
            if (IsInternalCommandFile(path))
                commandsService.ResolveCommand(path);

            workspaceService.UpdateDocument(path);
            return;
        }

        if (changeType == FileChangeType.Created && File.Exists(path)) {
            if (IsInternalCommandFile(path))
                commandsService.ResolveCommand(path);

            workspaceService.CreateDocument(path);
            return;
        }
    }

    public static bool IsInternalCommandFile(string filePath) {
        return Path.GetFileName(filePath) == "resolve.drc";
    }
}