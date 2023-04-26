using DotRush.Server.Processes;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotRush.Server.Services;

public class SolutionService {
    public static SolutionService Instance { get; private set; } = null!;
    public HashSet<string> ProjectFiles { get; private set; }
    public MSBuildWorkspace? Workspace { get; private set; }
    public Solution? Solution { get; private set; }
    public string? TargetFramework { get; set; }

    public Action<string>? ProjectLoaded;

    private SolutionService() {
        ProjectFiles = new HashSet<string>();
    }

    public static void Initialize(string[] targets) {
        MSBuildLocator.RegisterDefaults();

        Instance = new SolutionService();
        foreach (var target in targets)
            foreach (var path in Directory.GetFiles(target, "*.csproj", SearchOption.AllDirectories)) 
                Instance.ProjectFiles.Add(path);
    }

    public void UpdateSolution(Solution? solution) {
        Solution = solution;
    }
    public void AddTargets(string[] targets) {
        var added = new List<string>();
        foreach (var target in targets) 
            foreach (var path in Directory.GetFiles(target, "*.csproj", SearchOption.AllDirectories))
                if (ProjectFiles.Add(path))
                    added.Add(path);

        LoadProjects(added);
    }
    public void RemoveTargets(string[] targets) {
        var changed = false;
        foreach (var target in targets) 
            foreach (var path in Directory.GetFiles(target, "*.csproj", SearchOption.AllDirectories))
                changed = ProjectFiles.Remove(path);
        // MSBuildWorkspace does not support unloading projects
        if (changed) ForceReload();
    }
    public void ForceReload() {
        var configuration = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(TargetFramework))
            configuration.Add("TargetFramework", TargetFramework);

        Workspace = MSBuildWorkspace.Create(configuration);
        Workspace.LoadMetadataForReferencedProjects = true;
        Workspace.SkipUnrecognizedProjects = true;

        LoadProjects(ProjectFiles);
    }
    public Project? GetProjectByPath(string path) {
        return Solution?.Projects.FirstOrDefault(project => project.FilePath == path);
    }


    private void LoadProjects(IEnumerable<string> projectPaths) {
        foreach (var path in projectPaths) {
            try {
                RestoreProject(path);
                Workspace?.OpenProjectAsync(path).Wait();
                UpdateSolution(Workspace?.CurrentSolution);
                ProjectLoaded?.Invoke(path);
                LoggingService.Instance.LogMessage("Loaded project {0}", path);
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
}