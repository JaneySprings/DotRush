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
            switch (Path.GetExtension(path)) {
                case ".cs":
                    if (change.Type == FileChangeType.Created)
                        this.workspaceService.CreateCSharpDocument(path);
                    if (change.Type == FileChangeType.Deleted)
                        this.workspaceService.DeleteCSharpDocument(path);
                    break;
                case ".xaml":
                    if (change.Type == FileChangeType.Created)
                        this.workspaceService.CreateAdditionalDocument(path);
                    if (change.Type == FileChangeType.Deleted)
                        this.workspaceService.DeleteAdditionalDocument(path);
                    break;
                case "":
                    if (change.Type == FileChangeType.Created)
                        this.workspaceService.CreateFolder(path);
                    if (change.Type == FileChangeType.Deleted)
                        this.workspaceService.DeleteFolder(path);
                    break;
                case ".csproj":
                    serverFacade.Window.ShowWarning(string.Format(Resources.MessageProjectChanged, Path.GetFileName(path)));
                    break;
            }
        }

        return Unit.Task;
    }
}