using DotRush.Server.Extensions;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace DotRush.Server.Services;

public class SolutionService {
    public Solution? Solution { get; private set; }
    private HashSet<string> ProjectFiles { get; }
    private CancellationTokenSource? reloadCancellationTokenSource;
    private CancellationToken CancellationToken {
        get {
            this.reloadCancellationTokenSource?.Cancel();
            this.reloadCancellationTokenSource?.Dispose();
            this.reloadCancellationTokenSource = new CancellationTokenSource();
            return this.reloadCancellationTokenSource.Token;
        }
    }

    public SolutionService() {
        MSBuildLocator.RegisterDefaults();
        ProjectFiles = new HashSet<string>();
    }

    public async void ReloadSolution(IWorkDoneObserver? observer = null) {
        if (Solution != null) 
            Solution.Workspace?.Dispose();
    
        Solution = null;
        var workspace = MSBuildWorkspace.Create();
        var cancellationToken = CancellationToken;
        workspace.LoadMetadataForReferencedProjects = true;
        workspace.SkipUnrecognizedProjects = true;

        foreach (var path in ProjectFiles) {
            await ServerExtensions.SafeHandlerAsync(async () => {
                observer?.OnNext(new WorkDoneProgressReport { Message = $"Loading {Path.GetFileNameWithoutExtension(path)}" });
                await DotNetService.Instance.RestoreProjectAsync(path, cancellationToken);
                await workspace.OpenProjectAsync(path, cancellationToken: cancellationToken);
                UpdateSolution(workspace.CurrentSolution);
            });
        }

        observer?.OnCompleted();
    }
    public void UpdateSolution(Solution solution) {
        Solution = solution;
    }
    public void AddWorkspaceFolders(IEnumerable<string> workspaceFolders) {
        foreach (var folder in workspaceFolders) {
            if (!Directory.Exists(folder))
                continue;
            foreach (var projectFile in Directory.GetFiles(folder, "*.csproj", SearchOption.AllDirectories))
                ProjectFiles.Add(projectFile);
        }
    }
    public void RemoveWorkspaceFolders(IEnumerable<string> workspaceFolders) {
        foreach (var folder in workspaceFolders) {
            if (!Directory.Exists(folder))
                continue;
            foreach (var projectFile in Directory.GetFiles(folder, "*.csproj", SearchOption.AllDirectories))
                ProjectFiles.Remove(projectFile);
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
            UpdateSolution(updates.Project.Solution);
        }
    }
    public void DeleteCSharpDocument(string file) {
        var documentIds = Solution?.GetDocumentIdsWithFilePath(file);
        if (documentIds == null || Solution == null)
            return;

        foreach (var documentId in documentIds) {
            var document = Solution.GetDocument(documentId);
            var project = document?.Project;
            if (project == null || document == null)
                continue;

            var updates = project.RemoveDocument(documentId);
            UpdateSolution(updates.Solution);
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
            UpdateSolution(updatedDocument.Project.Solution);
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
            UpdateSolution(updates.Project.Solution);
        }
    }
    public void DeleteAdditionalDocument(string file) {
        var documentIds = Solution?.GetAdditionalDocumentIdsWithFilePath(file);
        if (documentIds == null || Solution == null)
            return;

        foreach (var documentId in documentIds) {
            var document = Solution.GetAdditionalDocument(documentId);
            var project = document?.Project;
            if (project == null || document == null)
                continue;

            var updates = project.RemoveAdditionalDocument(documentId);
            UpdateSolution(updates.Solution);
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

            UpdateSolution(updates);
        }
    }
}