using DotRush.Server.Processes;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotRush.Server.Services;

public class SolutionService {
    public HashSet<string> ProjectFiles { get; private set; }
    public MSBuildWorkspace? Workspace { get; private set; }
    public Solution? Solution { get; private set; }
    public string? TargetFramework { get; set; }

    public Action<string>? ProjectLoaded;

    public SolutionService(string[] targets) {
        ProjectFiles = new HashSet<string>();
        MSBuildLocator.RegisterDefaults();

        foreach (var target in targets)
            foreach (var path in Directory.GetFiles(target, "*.csproj", SearchOption.AllDirectories)) 
                ProjectFiles.Add(path);
    }

    public void UpdateSolution(Solution? solution) {
        Solution = solution;
    }
    public void AddTargets(string[] targets, CancellationToken cancellationToken) {
        var added = new List<string>();
        foreach (var target in targets) 
            foreach (var path in Directory.GetFiles(target, "*.csproj", SearchOption.AllDirectories))
                if (ProjectFiles.Add(path))
                    added.Add(path);

        LoadProjects(added, cancellationToken);
    }
    public void RemoveTargets(string[] targets, CancellationToken cancellationToken) {
        var changed = false;
        foreach (var target in targets) 
            foreach (var path in Directory.GetFiles(target, "*.csproj", SearchOption.AllDirectories))
                changed = ProjectFiles.Remove(path);
        // MSBuildWorkspace does not support unloading projects
        if (changed) 
            ForceReload(cancellationToken);
    }
    public void ForceReload(CancellationToken cancellationToken) {
        var configuration = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(TargetFramework))
            configuration.Add("TargetFramework", TargetFramework);

        Workspace = MSBuildWorkspace.Create(configuration);
        Workspace.LoadMetadataForReferencedProjects = true;
        Workspace.SkipUnrecognizedProjects = true;

        LoadProjects(ProjectFiles, cancellationToken);
    }
    public Project? GetProjectByPath(string path) {
        return Solution?.Projects.FirstOrDefault(project => project.FilePath == path);
    }


    private void LoadProjects(IEnumerable<string> projectPaths, CancellationToken cancellationToken) {
        foreach (var path in projectPaths) {
            RestoreProject(path);
            try {
                Workspace?.OpenProjectAsync(path, cancellationToken: cancellationToken).Wait();
                if (cancellationToken.IsCancellationRequested)
                    return;

                UpdateSolution(Workspace?.CurrentSolution);
                ProjectLoaded?.Invoke(path);
            } catch(Exception ex) {
                LoggingService.Instance.LogError(ex.Message, ex);
            }
        }
    }

    private void RestoreProject(string path) {
        var result = new ProcessRunner("dotnet", new ProcessArgumentBuilder()
            .Append("restore")
            .AppendQuoted(path))
            .WaitForExit();

        LoggingService.Instance.LogMessage("Restored project {0}", path);
    }

    // Extensions

    public Document? GetDocumentByPath(string? path) {
        if (string.IsNullOrEmpty(path)) 
            return null;
        var documentId = Solution?
            .GetDocumentIdsWithFilePath(path)
            .FirstOrDefault();
        return Solution?.GetDocument(documentId);
    }
    public IEnumerable<Document>? GetDocumentsByDirectoryPath(string? path) {
        if (string.IsNullOrEmpty(path)) 
            return null;

        if (!path.EndsWith(Path.DirectorySeparatorChar))
            path += Path.DirectorySeparatorChar;
        
        var project = GetProjectByDocumentPath(path);
        if (project == null) 
            return null;

        return project.Documents.Where(it => it.FilePath!.StartsWith(path));
    }
    public Project? GetProjectByDocumentPath(string path) {
        var targetProjectLocation = ProjectFiles
            .Select(it => Path.GetDirectoryName(it) + Path.DirectorySeparatorChar)
            .FirstOrDefault(it => path.StartsWith(it));
        
        if (targetProjectLocation == null) 
            return null;

        return Solution?.Projects.FirstOrDefault(it => it.FilePath!.StartsWith(targetProjectLocation));
    }
}