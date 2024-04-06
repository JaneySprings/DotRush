using DotRush.Roslyn.Server.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using FileSystemExtensions = DotRush.Roslyn.Server.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.Server.Services;

public abstract class SolutionService: ProjectService {
    public Solution? Solution { get; private set; }

    protected async Task LoadSolutionAsync(MSBuildWorkspace workspace) {
        await LoadAsync(workspace, UpdateCurrentSolution);
    }
    private void UpdateCurrentSolution(Solution? solution) {
        Solution = solution;
    }

    public void DeleteFolder(string path) {
        var csharpDocumentIds = Solution?.GetDocumentIdsWithFolderPath(path);
        var additionalDocumentIds = Solution?.GetAdditionalDocumentIdsWithFolderPath(path);
        DeleteSourceCodeDocument(csharpDocumentIds);
        DeleteAdditionalDocument(additionalDocumentIds);
    }

    public void CreateSourceCodeDocument(string file) {
        var projectIds = Solution?.GetProjectIdsMayContainsFilePath(file);
        if (projectIds == null || Solution == null)
            return;

        foreach (var projectId in projectIds) {
            var project = Solution.GetProject(projectId);
            if (project?.FilePath == null || !File.Exists(file))
                continue;

            if (!FileSystemExtensions.IsFileVisible(file, project))
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
    public void DeleteSourceCodeDocument(string file) {
        var documentIds = Solution?.GetDocumentIdsWithFilePath(file);
        DeleteSourceCodeDocument(documentIds);
    }
    public void UpdateSourceCodeDocument(string file, string? text = null) {
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

    public void CreateAdditionalDocument(string file) {
        var projectIds = Solution?.GetProjectIdsMayContainsFilePath(file);
        if (projectIds == null || Solution == null)
            return;

        foreach (var projectId in projectIds) {
            var project = Solution.GetProject(projectId);
            if (project?.FilePath == null || !File.Exists(file))
                continue;

            if (!FileSystemExtensions.IsFileVisible(file, project))
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
    public void DeleteAdditionalDocument(string file) {
        var documentIds = Solution?.GetAdditionalDocumentIdsWithFilePath(file);
        DeleteAdditionalDocument(documentIds);
    }
    public void UpdateAdditionalDocument(string file, string? text = null) {
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

    public void CreateDocument(string file) {
        if (LanguageServer.IsAdditionalDocument(file))
            CreateAdditionalDocument(file);
        if (LanguageServer.IsSourceCodeDocument(file))
            CreateSourceCodeDocument(file);
    }
    public void DeleteDocument(string file) {
        if (LanguageServer.IsAdditionalDocument(file))
            DeleteAdditionalDocument(file);
        if (LanguageServer.IsSourceCodeDocument(file))
            DeleteSourceCodeDocument(file);
    }
    public void UpdateDocument(string file, string? text = null) {
        if (LanguageServer.IsAdditionalDocument(file))
            UpdateAdditionalDocument(file, text);
        if (LanguageServer.IsSourceCodeDocument(file))
            UpdateSourceCodeDocument(file, text);
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