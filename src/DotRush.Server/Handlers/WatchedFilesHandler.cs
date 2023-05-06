using DotRush.Server.Services;
using MediatR;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace DotRush.Server.Handlers;

public class WatchedFilesHandler : DidChangeWatchedFilesHandlerBase {
    private SolutionService solutionService;

    public WatchedFilesHandler(SolutionService solutionService) {
        this.solutionService = solutionService;
    }

    protected override DidChangeWatchedFilesRegistrationOptions CreateRegistrationOptions(DidChangeWatchedFilesCapability capability, ClientCapabilities clientCapabilities) {
        return new DidChangeWatchedFilesRegistrationOptions();
    }

    public override Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken) {
        foreach (var change in request.Changes) {
            if (change.Type != FileChangeType.Created && change.Type != FileChangeType.Deleted) 
                continue;

            var path = change.Uri.GetFileSystemPath();
            var project = this.solutionService.GetProjectByDocumentPath(path);
            if (project == null) 
                continue;

            if (Directory.Exists(path) && change.Type == FileChangeType.Created) {
                var documents = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
                foreach (var document in documents) 
                    project = CreateFile(document, project);

                continue;
            }

            if (!path.EndsWith(".cs") && change.Type == FileChangeType.Deleted) {
                var documents = this.solutionService.GetDocumentsByDirectoryPath(path)?.Select(it => it.FilePath!);
                if (documents == null) 
                    continue;

                foreach (var document in documents) 
                    project = DeleteFile(document, project);

                continue;
            }

            if (change.Type == FileChangeType.Created)
                project = CreateFile(path, project);
            if (change.Type == FileChangeType.Deleted)
                project = DeleteFile(path, project);
        }

        return Unit.Task;
    }

    private Project CreateFile(string path, Project project) {
        var documentContent = File.ReadAllText(path);
        var folders = GetFolders(project.FilePath!, path);
        var updates = project.AddDocument(Path.GetFileName(path), documentContent, folders, path);
        this.solutionService.UpdateSolution(updates.Project.Solution);
        return updates.Project;
    }
    private Project DeleteFile(string path, Project project) {
        var document = this.solutionService.GetDocumentByPath(path);
        var updates = project.RemoveDocument(document!.Id);
        this.solutionService.UpdateSolution(updates.Solution);
        return updates;
    }
    private IEnumerable<string> GetFolders(string projectPath, string documentPath) {
        var rootDirectory = Path.GetDirectoryName(projectPath)!;
        var documentDirectory = Path.GetDirectoryName(documentPath)!;
        var relativePath = documentDirectory.Replace(rootDirectory, string.Empty);
        return relativePath.Split(Path.DirectorySeparatorChar).Where(it => !string.IsNullOrEmpty(it));
    }
}