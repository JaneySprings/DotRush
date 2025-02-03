using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using FileSystemExtensions = DotRush.Roslyn.Common.Extensions.FileSystemExtensions;

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
                OnProjectLoadCompleted(solutionFile);

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
        CurrentSessionLogger.Debug($"Projects loading completed, loaded {workspace.CurrentSolution.ProjectIds.Count} projects");
    }

    public void DeleteFolder(string path) {
        var csharpDocumentIds = Solution?.GetDocumentIdsWithFolderPath(path);
        var additionalDocumentIds = Solution?.GetAdditionalDocumentIdsWithFolderPath(path);
        DeleteSourceCodeDocument(csharpDocumentIds);
        DeleteAdditionalDocument(additionalDocumentIds);
    }
    public void CreateDocument(string file) {
        if (LanguageExtensions.IsAdditionalDocument(file))
            CreateAdditionalDocument(file);
        if (LanguageExtensions.IsSourceCodeDocument(file))
            CreateSourceCodeDocument(file);
    }
    public void DeleteDocument(string file) {
        if (LanguageExtensions.IsAdditionalDocument(file))
            DeleteAdditionalDocument(file);
        if (LanguageExtensions.IsSourceCodeDocument(file))
            DeleteSourceCodeDocument(file);
    }
    public void UpdateDocument(string file, string? text = null) {
        if (LanguageExtensions.IsAdditionalDocument(file))
            UpdateAdditionalDocument(file, text);
        if (LanguageExtensions.IsSourceCodeDocument(file))
            UpdateSourceCodeDocument(file, text);
    }

    private void CreateSourceCodeDocument(string file) {
        var projectIds = Solution?.GetProjectIdsMayContainsFilePath(file);
        if (projectIds == null || Solution == null)
            return;

        foreach (var projectId in projectIds) {
            var project = Solution.GetProject(projectId);
            if (project?.FilePath == null || !File.Exists(file))
                continue;

            if (!FileSystemExtensions.IsFileVisible(Path.GetDirectoryName(project.FilePath), project.GetFolders(file), Path.GetFileName(file)))
                continue;
            if (project.GetDocumentIdsWithFilePath(file).Any())
                continue;

            var sourceText = SourceText.From(File.ReadAllText(file));
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

        var sourceText = SourceText.From(text ?? File.ReadAllText(file));
        foreach (var documentId in documentIds) {
            var document = Solution?.GetDocument(documentId);
            if (document == null || document.Project == null)
                continue;

            var updatedDocument = document.WithText(sourceText);
            OnWorkspaceStateChanged(updatedDocument.Project.Solution);
        }
    }
    private void CreateAdditionalDocument(string file) {
        var projectIds = Solution?.GetProjectIdsMayContainsFilePath(file);
        if (projectIds == null || Solution == null)
            return;

        foreach (var projectId in projectIds) {
            var project = Solution.GetProject(projectId);
            if (project?.FilePath == null || !File.Exists(file))
                continue;

            if (!FileSystemExtensions.IsFileVisible(Path.GetDirectoryName(project.FilePath), project.GetFolders(file), Path.GetFileName(file)))
                continue;
            if (project.GetAdditionalDocumentIdsWithFilePath(file).Any())
                continue;

            var sourceText = SourceText.From(File.ReadAllText(file));
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

        var sourceText = SourceText.From(text ?? File.ReadAllText(file));
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

            var updates = project.RemoveAdditionalDocument(documentId);
            OnWorkspaceStateChanged(updates.Solution);
        }
    }
}
