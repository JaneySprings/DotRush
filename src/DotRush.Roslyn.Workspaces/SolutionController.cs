using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using FileSystemExtensions = DotRush.Roslyn.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.Workspaces;

public abstract class SolutionController : ProjectsController {
    public Solution? Solution { get; private set; }

    protected override void OnWorkspaceStateChanged(MSBuildWorkspace workspace) {
        Solution = workspace.CurrentSolution;
    }

    protected async Task LoadSolutionAsync(MSBuildWorkspace workspace, CancellationToken cancellationToken) {
        await LoadAsync(workspace, cancellationToken);
    }

    public void DeleteFolder(string path) {
        var csharpDocumentIds = Solution?.GetDocumentIdsWithFolderPath(path);
        var additionalDocumentIds = Solution?.GetAdditionalDocumentIdsWithFolderPath(path);
        DeleteSourceCodeDocument(csharpDocumentIds);
        DeleteAdditionalDocument(additionalDocumentIds);
    }
    public void CreateDocument(string file) {
        if (ProjectsController.IsAdditionalDocument(file))
            CreateAdditionalDocument(file);
        if (ProjectsController.IsSourceCodeDocument(file))
            CreateSourceCodeDocument(file);
    }
    public void DeleteDocument(string file) {
        if (ProjectsController.IsAdditionalDocument(file))
            DeleteAdditionalDocument(file);
        if (ProjectsController.IsSourceCodeDocument(file))
            DeleteSourceCodeDocument(file);
    }
    public void UpdateDocument(string file, string? text = null) {
        if (ProjectsController.IsAdditionalDocument(file))
            UpdateAdditionalDocument(file, text);
        if (ProjectsController.IsSourceCodeDocument(file))
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
            if (file.StartsWith(project.GetIntermediateOutputPath()) || file.StartsWith(project.GetOutputPath()))
                continue;
            if (project.GetDocumentIdWithFilePath(file) != null)
                continue;

            var sourceText = SourceText.From(File.ReadAllText(file));
            var folders = project.GetFolders(file);
            var updates = project.AddDocument(Path.GetFileName(file), sourceText, folders, file);
            Solution = updates.Project.Solution;
        }
    }
    private void DeleteSourceCodeDocument(string file) {
        var documentIds = Solution?.GetDocumentIdsWithFilePath(file);
        DeleteSourceCodeDocument(documentIds);
    }
    private void UpdateSourceCodeDocument(string file, string? text = null) {
        var documentIds = Solution?.GetDocumentIdsWithFilePath(file);
        if (documentIds == null || !File.Exists(file))
            return;

        var sourceText = SourceText.From(text ?? File.ReadAllText(file));
        foreach (var documentId in documentIds) {
            var document = Solution?.GetDocument(documentId);
            if (document == null || document.Project == null)
                continue;

            if (file.StartsWith(document.Project.GetIntermediateOutputPath()) ||
                file.StartsWith(document.Project.GetOutputPath()))
                continue;

            var updatedDocument = document.WithText(sourceText);
            Solution = updatedDocument.Project.Solution;
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
            if (file.StartsWith(project.GetIntermediateOutputPath()) || file.StartsWith(project.GetOutputPath()))
                continue;
            if (project.GetAdditionalDocumentIdWithFilePath(file) != null)
                continue;

            var sourceText = SourceText.From(File.ReadAllText(file));
            var folders = project.GetFolders(file);
            var updates = project.AddAdditionalDocument(Path.GetFileName(file), sourceText, folders, file);
            Solution = updates.Project.Solution;
        }
    }
    private void DeleteAdditionalDocument(string file) {
        var documentIds = Solution?.GetAdditionalDocumentIdsWithFilePath(file);
        DeleteAdditionalDocument(documentIds);
    }
    private void UpdateAdditionalDocument(string file, string? text = null) {
        var documentIds = Solution?.GetDocumentIdsWithFilePath(file);
        if (documentIds == null || !File.Exists(file))
            return;

        var sourceText = SourceText.From(text ?? File.ReadAllText(file));
        foreach (var documentId in documentIds) {
            var project = Solution?.GetProject(documentId.ProjectId);
            if (project == null)
                continue;

            if (file.StartsWith(project.GetIntermediateOutputPath()) ||
                file.StartsWith(project.GetOutputPath()))
                continue;

            var updates = Solution?.WithAdditionalDocumentText(documentId, sourceText);
            if (updates == null)
                continue;

            Solution = updates;
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

            if (document.FilePath.StartsWith(project.GetIntermediateOutputPath()) ||
                document.FilePath.StartsWith(project.GetOutputPath()))
                continue;

            var updates = project.RemoveDocument(documentId);
            Solution = updates.Solution;
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

            if (document.FilePath.StartsWith(project.GetIntermediateOutputPath()) ||
                document.FilePath.StartsWith(project.GetOutputPath()))
                continue;

            var updates = project.RemoveAdditionalDocument(documentId);
            Solution = updates.Solution;
        }
    }
}
