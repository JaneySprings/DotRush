using DotRush.Server.Extensions;
using DotRush.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
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
                    Kind = WatchKind.Create | WatchKind.Delete | WatchKind.Change,
                    GlobPattern = "**/*"
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
                        CreateFolder(path);
                    if (change.Type == FileChangeType.Deleted)
                        DeleteFolder(path);
                    break;
                case ".csproj":
                    if (change.Type != FileChangeType.Changed)
                        break;
                    this.workspaceService.StartSolutionReloading();
                    return Unit.Task;
            }
        }

        return Unit.Task;
    }

    private void DeleteFolder(string path) {
        var csharpDocumentIds = this.workspaceService.Solution?.GetDocumentIdsWithFolderPath(path);
        var additionalDocumentIds = this.workspaceService.Solution?.GetAdditionalDocumentIdsWithFolderPath(path);
        this.workspaceService.DeleteCSharpDocument(csharpDocumentIds);
        this.workspaceService.DeleteAdditionalDocument(additionalDocumentIds);
    }
    private void CreateFolder(string path) {
        if (!Directory.Exists(path))
            return;

        var csharpDocuments = WorkspaceExtensions.GetFilesFromVisibleFolders(path, "*.cs");
        var additionalDocuments = WorkspaceExtensions.GetFilesFromVisibleFolders(path, "*.xaml");

        foreach (var file in csharpDocuments)
            this.workspaceService.CreateCSharpDocument(file);
        foreach (var file in additionalDocuments)
            this.workspaceService.CreateAdditionalDocument(file);
    }
}