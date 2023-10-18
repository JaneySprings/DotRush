using DotRush.Server.Extensions;
using DotRush.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace DotRush.Server.Handlers;

public class WatchedFilesHandler : DidChangeWatchedFilesHandlerBase {
    private readonly WorkspaceService workspaceService;
    private readonly CompilationService compilationService;
    private readonly ILanguageServerFacade serverFacade;

    public WatchedFilesHandler(ILanguageServerFacade serverFacade, WorkspaceService workspaceService, CompilationService compilationService) {
        this.workspaceService = workspaceService;
        this.compilationService = compilationService;
        this.serverFacade = serverFacade;
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
                foreach (var filePath in WorkspaceExtensions.GetVisibleFiles(path, "*"))
                    HandleFileChange(filePath, FileChangeType.Created);
            }
        }

        return Unit.Task;
    }

    private void HandleFileChange(string path, FileChangeType changeType) {
        var extension = Path.GetExtension(path);
        if (extension == ".csproj" && changeType == FileChangeType.Changed)
            return; // Handled from IDE

        if (changeType == FileChangeType.Deleted) {
            if (extension == ".cs") {
                workspaceService.DeleteCSharpDocument(path);
                return;
            }
            workspaceService.DeleteAdditionalDocument(path);
            workspaceService.DeleteFolder(path);
            return;
        }

        if (changeType == FileChangeType.Created && File.Exists(path)) {
            if (extension == ".cs") {
                this.workspaceService.CreateCSharpDocument(path);
                return;
            }
            if (extension == ".xaml" /* add other supported ext*/) {
                this.workspaceService.CreateAdditionalDocument(path);
                return;
            }
        } 
    }
}