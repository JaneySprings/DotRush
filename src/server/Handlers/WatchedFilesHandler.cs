using System.Collections.Immutable;
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
                    Kind = WatchKind.Create | WatchKind.Delete,
                    GlobPattern = "**/*"
                },
            }
        };
    }

    public override Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken) {
        // TODO: Use relative path
        if (request.Changes.Any(it => it.Uri.GetFileSystemPath().Split(Path.DirectorySeparatorChar).Any(it => it.StartsWith("."))))
            return Unit.Task;

        foreach (var change in request.Changes) {
            var path = change.Uri.GetFileSystemPath();
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
            }
        }

        return Unit.Task;
    }

    private void DeleteFolder(string path) {
        var projectIds = solutionService.Solution?.GetProjectIdsWithDocumentFolderPath(path);
        if (projectIds == null || solutionService.Solution == null)
            return;

        foreach (var projectId in projectIds) {
            var project = solutionService.Solution.GetProject(projectId);
            if (project == null)
                continue;

            var documentIds = project.GetDocumentIdsWithFolderPath(path);
            var updates = project.RemoveDocuments(ImmutableArray.Create(documentIds.ToArray()));
            solutionService.UpdateSolution(updates.Solution);
        }
    }
    private void CreateFolder(string path) {
        if (!Directory.Exists(path))
            return;

        var csharpDocuments = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
        var additionalDocuments = Directory.GetFiles(path, "*.xaml", SearchOption.AllDirectories);

        foreach (var file in csharpDocuments)
            this.solutionService.CreateCSharpDocument(file);
        foreach (var file in additionalDocuments)
            this.solutionService.CreateAdditionalDocument(file);
    }
}