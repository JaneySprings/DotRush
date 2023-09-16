using DotRush.Server.Extensions;
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
        return new DidChangeWatchedFilesRegistrationOptions() {
            Watchers = new[] {
                new OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher() {
                    Kind = WatchKind.Create | WatchKind.Delete | WatchKind.Change,
                    GlobPattern = "**/*"
                },
            }
        };
    }

    public override async Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken) {
        foreach (var change in request.Changes) {
            var path = change.Uri.GetFileSystemPath();
            var pathSegments = path.Split(Path.DirectorySeparatorChar);
            if (pathSegments.Any(it => it.StartsWith(".")))
                continue; // TODO: Use relative paths instead of absolute paths

            switch (Path.GetExtension(path)) {
                case ".cs":
                    if (change.Type == FileChangeType.Created)
                        this.solutionService.CreateCSharpDocument(path);
                    if (change.Type == FileChangeType.Deleted)
                        this.solutionService.DeleteCSharpDocument(path);
                    break;
                case ".xaml":
                    if (change.Type == FileChangeType.Created)
                        this.solutionService.CreateAdditionalDocument(path);
                    if (change.Type == FileChangeType.Deleted)
                        this.solutionService.DeleteAdditionalDocument(path);
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
                    var observer = await LanguageServer.CreateWorkDoneObserverAsync();
                    this.solutionService.StartSolutionReloading(observer);
                    return Unit.Value;
            }
        }

        return Unit.Value;
    }

    private void DeleteFolder(string path) {
        var csharpDocumentIds = this.solutionService.Solution?.GetDocumentIdsWithFolderPath(path);
        var additionalDocumentIds = this.solutionService.Solution?.GetAdditionalDocumentIdsWithFolderPath(path);
        this.solutionService.DeleteCSharpDocument(csharpDocumentIds);
        this.solutionService.DeleteAdditionalDocument(additionalDocumentIds);
    }
    private void CreateFolder(string path) {
        if (!Directory.Exists(path))
            return;

        var csharpDocuments = WorkspaceExtensions.GetFilesFromVisibleFolders(path, "*.cs");
        var additionalDocuments = WorkspaceExtensions.GetFilesFromVisibleFolders(path, "*.xaml");

        foreach (var file in csharpDocuments)
            this.solutionService.CreateCSharpDocument(file);
        foreach (var file in additionalDocuments)
            this.solutionService.CreateAdditionalDocument(file);
    }
}