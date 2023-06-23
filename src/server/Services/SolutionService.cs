using DotRush.Server.Extensions;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace DotRush.Server.Services;

public class SolutionService {
    public Solution? Solution { get; private set; }
    private ProjectService ProjectService { get; }

    public SolutionService() {
        MSBuildLocator.RegisterDefaults();
        ProjectService = new ProjectService();
        ProjectService.WorkspaceUpdated = s => Solution = s;
    }

    public async void ReloadSolutionAsync(IWorkDoneObserver? observer = null, bool forceRestore = false) {
        await ProjectService.ReloadAsync(observer, forceRestore);
    }
    public void AddWorkspaceFolders(IEnumerable<string> workspaceFolders) {
        foreach (var folder in workspaceFolders) {
            if (!Directory.Exists(folder))
                continue;
            foreach (var projectFile in Directory.GetFiles(folder, "*.csproj", SearchOption.AllDirectories))
                ProjectService.Projects.Add(projectFile);
        }
    }
    public void RemoveWorkspaceFolders(IEnumerable<string> workspaceFolders) {
        foreach (var folder in workspaceFolders) {
            if (!Directory.Exists(folder))
                continue;
            foreach (var projectFile in Directory.GetFiles(folder, "*.csproj", SearchOption.AllDirectories))
                ProjectService.Projects.Remove(projectFile);
        }
    }

    public void CreateCSharpDocument(string file) {
        var projectIds = Solution?.GetProjectIdsMayContainsFilePath(file);
        if (projectIds == null || Solution == null)
            return;

        foreach (var projectId in projectIds) {
            var project = Solution.GetProject(projectId);
            if (project == null || !File.Exists(file))
                continue;

            var documentIds = project.GetDocumentIdsWithFolderPath(file);
            if (documentIds.Any())
                continue;

            var sourceText = SourceText.From(File.ReadAllText(file));
            var folders = project.GetFolders(file);
            var updates = project.AddDocument(Path.GetFileName(file), sourceText, folders, file);
            Solution = updates.Project.Solution;
        }
    }
    public void DeleteCSharpDocument(string file) {
        var documentIds = Solution?.GetDocumentIdsWithFilePath(file);
        DeleteCSharpDocument(documentIds);
    }
    public void DeleteCSharpDocument(IEnumerable<DocumentId>? documentIds) {
        if (documentIds == null || Solution == null)
            return;

        foreach (var documentId in documentIds) {
            var document = Solution.GetDocument(documentId);
            var project = document?.Project;
            if (project == null || document == null)
                continue;

            var updates = project.RemoveDocument(documentId);
            Solution = updates.Solution;
        }
    }
    public void UpdateCSharpDocument(string file, string? text = null) {
        var documentIds = Solution?.GetDocumentIdsWithFilePath(file);
        if (documentIds == null || !File.Exists(file))
            return;

        var sourceText = SourceText.From(text ?? File.ReadAllText(file));
        foreach (var documentId in documentIds) {
            var document = Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var updatedDocument = document.WithText(sourceText);
            Solution = updatedDocument.Project.Solution;
        }
    }

    public void CreateAdditionalDocument(string file) {
        var projectIds = Solution?.GetProjectIdsMayContainsFilePath(file);
        if (projectIds == null || Solution == null)
            return;

        foreach (var projectId in projectIds) {
            var project = Solution.GetProject(projectId);
            if (project == null || !File.Exists(file))
                continue;

            var documentIds = project.GetAdditionalDocumentIdsWithFilePath(file);
            if (documentIds.Any())
                continue;

            var sourceText = SourceText.From(File.ReadAllText(file));
            var folders = project.GetFolders(file);
            var updates = project.AddAdditionalDocument(Path.GetFileName(file), sourceText, folders, file);
            Solution = updates.Project.Solution;
        }
    }
    public void DeleteAdditionalDocument(string file) {
        var documentIds = Solution?.GetAdditionalDocumentIdsWithFilePath(file);
        DeleteAdditionalDocument(documentIds);
    }
    public void DeleteAdditionalDocument(IEnumerable<DocumentId>? documentIds) {
        if (documentIds == null || Solution == null)
            return;

        foreach (var documentId in documentIds) {
            var document = Solution.GetAdditionalDocument(documentId);
            var project = document?.Project;
            if (project == null || document == null)
                continue;

            var updates = project.RemoveAdditionalDocument(documentId);
            Solution = updates.Solution;
        }
    }
    public void UpdateAdditionalDocument(string file, string? text = null) {
        var documentIds = Solution?.GetDocumentIdsWithFilePath(file);
        if (documentIds == null || !File.Exists(file))
            return;

        var sourceText = SourceText.From(text ?? File.ReadAllText(file));
        foreach (var documentId in documentIds) {
            var updates = Solution?.WithAdditionalDocumentText(documentId, sourceText);
            if (updates == null)
                return;

            Solution = updates;
        }
    }
}