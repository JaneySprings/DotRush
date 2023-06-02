using DotRush.Server.Extensions;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Server.Services;

public class SolutionService {
    public Solution? Solution { get; private set; }
    private HashSet<string> ProjectFiles { get; }


    public SolutionService(string[] targets) {
        MSBuildLocator.RegisterDefaults();
        ProjectFiles = new HashSet<string>();

        foreach (var target in targets) 
            AddProjects(Directory.GetFiles(target, "*.csproj", SearchOption.AllDirectories));
    }


    public async void ReloadSolution(Action<string>? onComplete = null) {
        if (Solution != null) 
            Solution.Workspace?.Dispose();
    
        Solution = null;
        var workspace = MSBuildWorkspace.Create();
        workspace.LoadMetadataForReferencedProjects = true;
        workspace.SkipUnrecognizedProjects = true;

        foreach (var path in ProjectFiles) {
            try {
                await workspace.OpenProjectAsync(path);
                UpdateSolution(workspace.CurrentSolution);
                onComplete?.Invoke(path);
            } catch(Exception ex) {
                LoggingService.Instance.LogError(ex.Message, ex);
            }
        }
    }
    public void UpdateSolution(Solution solution) {
        Solution = solution;
    }
    public void AddProjects(IEnumerable<string> projectFilePaths) {
        foreach (var path in projectFilePaths)
            ProjectFiles.Add(path);
    }
    public void RemoveProjects(IEnumerable<string> projectFilePaths) {
        foreach (var path in projectFilePaths)
            ProjectFiles.Remove(path);
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
}