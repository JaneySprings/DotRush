using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using FileSystemExtensions = DotRush.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.Workspaces;

public abstract class SolutionController : ProjectsController {
    public Solution? Solution { get; protected set; }
    public event EventHandler? WorkspaceStateChanged;

    protected override void OnWorkspaceStateChanged(Solution newSolution) {
        Solution = newSolution;
        WorkspaceStateChanged?.Invoke(this, EventArgs.Empty);
    }

    protected async Task LoadSolutionAsync(MSBuildWorkspace workspace, IEnumerable<string> solutionFilePaths, CancellationToken cancellationToken) {
        CurrentSessionLogger.Debug($"Loading solutions: {string.Join(';', solutionFilePaths)}");
        await OnLoadingStartedAsync(cancellationToken);

        foreach (var solutionFile in solutionFilePaths) {
            await SafeExtensions.InvokeAsync(async () => {
                if (RestoreProjectsBeforeLoading) {
                    OnProjectRestoreStarted(solutionFile);
                    var result = await workspace.RestoreProjectAsync(solutionFile, cancellationToken);
                    if (result.ExitCode != 0)
                        OnProjectRestoreFailed(solutionFile, result);
                    OnProjectRestoreCompleted(solutionFile);
                }

                OnProjectLoadStarted(solutionFile);
                var solution = await workspace.OpenSolutionAsync(solutionFile, null, cancellationToken);
                solution.Projects.ForEach(project => OnProjectLoadCompleted(project.FilePath ?? string.Empty));

                OnWorkspaceStateChanged(workspace.CurrentSolution);

                if (CompileProjectsAfterLoading) {
                    foreach (var project in solution.Projects) {
                        OnProjectCompilationStarted(project.FilePath ?? solutionFile);
                        _ = await project.GetCompilationAsync(cancellationToken);
                        OnProjectCompilationCompleted(project.FilePath ?? solutionFile);
                    }
                }
            });
        }

        await OnLoadingCompletedAsync(cancellationToken);
        CurrentSessionLogger.Debug($"Solution loading completed, loaded {workspace.CurrentSolution.ProjectIds.Count} projects");
    }

    public void CreateDocuments(string[] files) {
        foreach (var file in files)
            CreateDocument(file);
    }
    public void CreateDocument(string file) {
        if (IsAdditionalDocument(file))
            CreateAdditionalDocument(file);
        if (IsSourceCodeDocument(file))
            CreateSourceCodeDocument(file);
    }
    public void UpdateDocument(string file, string? text = null) {
        if (IsAdditionalDocument(file))
            UpdateAdditionalDocument(file, text);
        if (IsSourceCodeDocument(file))
            UpdateSourceCodeDocument(file, text);
    }
    public void DeleteDocuments(string[] files) {
        foreach (var file in files)
            DeleteDocument(file);
    }
    public void DeleteDocument(string file) {
        if (IsAdditionalDocument(file))
            DeleteAdditionalDocument(file);
        if (IsSourceCodeDocument(file))
            DeleteSourceCodeDocument(file);
    }

    public static bool IsSourceCodeDocument(string filePath) {
        var allowedExtensions = new[] { ".cs", /* .fs .vb */};
        return allowedExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsAdditionalDocument(string filePath) {
        var allowedExtensions = new[] { ".xaml", /* maybe '.razor' ? */};
        return allowedExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsProjectFile(string filePath) {
        var allowedExtensions = new[] { ".csproj", /* fsproj vbproj */};
        return allowedExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }

    private void CreateSourceCodeDocument(string file) {
        if (Solution != null && Solution.GetDocumentIdsWithFilePathV2(file).Any())
            return;
        var projectIds = Solution?.GetProjectIdsMayContainsFilePath(file);
        if (projectIds == null || Solution == null)
            return;

        foreach (var projectId in projectIds) {
            var project = Solution.GetProject(projectId);
            if (project?.FilePath == null || !File.Exists(file))
                continue;

            if (!CanBeProcessed(file, project))
                continue;

            var sourceText = SourceText.From(FileSystemExtensions.TryReadText(file));
            var folders = project.GetFolders(file);
            var updates = project.AddDocument(Path.GetFileName(file), sourceText, folders, file);
            OnWorkspaceStateChanged(updates.Project.Solution);
        }
    }
    private void DeleteSourceCodeDocument(string file) {
        var documentIds = Solution?.GetDocumentIdsWithFilePathV2(file);
        DeleteSourceCodeDocument(documentIds);
    }
    private void UpdateSourceCodeDocument(string file, string? text = null) {
        var documentIds = Solution?.GetDocumentIdsWithFilePathV2(file);
        if (documentIds == null || !File.Exists(file))
            return;

        var sourceText = SourceText.From(text ?? FileSystemExtensions.TryReadText(file));
        foreach (var documentId in documentIds) {
            var document = Solution?.GetDocument(documentId);
            if (document == null || document.Project == null)
                continue;

            var updatedDocument = document.WithText(sourceText);
            OnWorkspaceStateChanged(updatedDocument.Project.Solution);
        }
    }
    private void CreateAdditionalDocument(string file) {
        if (Solution != null && Solution.GetAdditionalDocumentIdsWithFilePathV2(file).Any())
            return;
        var projectIds = Solution?.GetProjectIdsMayContainsFilePath(file);
        if (projectIds == null || Solution == null)
            return;

        foreach (var projectId in projectIds) {
            var project = Solution.GetProject(projectId);
            if (project?.FilePath == null || !File.Exists(file))
                continue;

            if (!CanBeProcessed(file, project))
                continue;

            var sourceText = SourceText.From(FileSystemExtensions.TryReadText(file));
            var folders = project.GetFolders(file);
            var updates = project.AddAdditionalDocument(Path.GetFileName(file), sourceText, folders, file);
            OnWorkspaceStateChanged(updates.Project.Solution);
        }
    }
    private void DeleteAdditionalDocument(string file) {
        var documentIds = Solution?.GetAdditionalDocumentIdsWithFilePathV2(file);
        DeleteAdditionalDocument(documentIds);
    }
    private void UpdateAdditionalDocument(string file, string? text = null) {
        var documentIds = Solution?.GetAdditionalDocumentIdsWithFilePathV2(file);
        if (documentIds == null || !File.Exists(file))
            return;

        var sourceText = SourceText.From(text ?? FileSystemExtensions.TryReadText(file));
        foreach (var documentId in documentIds) {
            var project = Solution?.GetProject(documentId.ProjectId);
            if (project == null)
                continue;

            var updates = Solution?.WithAdditionalDocumentText(documentId, sourceText);
            if (updates == null)
                continue;

            OnWorkspaceStateChanged(updates);
        }
    }
    private void DeleteSourceCodeDocument(IEnumerable<DocumentId>? documentIds) {
        if (documentIds == null || Solution == null)
            return;

        foreach (var documentId in documentIds) {
            var document = Solution.GetDocument(documentId);
            var project = document?.Project;
            if (project == null || document?.FilePath == null)
                continue;

            if (!CanBeProcessed(document.FilePath, project))
                continue;

            var updates = project.RemoveDocument(documentId);
            OnWorkspaceStateChanged(updates.Solution);
        }
    }
    private void DeleteAdditionalDocument(IEnumerable<DocumentId>? documentIds) {
        if (documentIds == null || Solution == null)
            return;

        foreach (var documentId in documentIds) {
            var document = Solution.GetAdditionalDocument(documentId);
            var project = document?.Project;
            if (project == null || document?.FilePath == null)
                continue;

            if (!CanBeProcessed(document.FilePath, project))
                continue;

            var updates = project.RemoveAdditionalDocument(documentId);
            OnWorkspaceStateChanged(updates.Solution);
        }
    }

    private bool CanBeProcessed(string file, Project project) {
        if (PathExtensions.StartsWith(file, project.GetIntermediateOutputPath()))
            return false;

        var folders = project.GetFolders(file);
        if (folders.Any() && folders.Any(f => f.StartsWith('.')))
            return false;

        return true;
    }
}
