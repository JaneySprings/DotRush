using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using DotRush.Server.Processes;

namespace DotRush.Server.Services;

public class SolutionService {
    public static SolutionService Instance { get; private set; } = null!;
    public HashSet<string> ProjectFiles { get; private set; }
    public MSBuildWorkspace? Workspace { get; private set; }
    public Solution? Solution { get; private set; }
    private string? targetFramework;


    private SolutionService() {
        ProjectFiles = new HashSet<string>();
    }

    public static void Initialize(string[] targets) {
        var queryOptions = VisualStudioInstanceQueryOptions.Default;
        var instances = MSBuildLocator.QueryVisualStudioInstances(queryOptions);
        MSBuildLocator.RegisterInstance(instances.FirstOrDefault());

        Instance = new SolutionService();
        foreach (var target in targets)
            foreach (var path in Directory.GetFiles(target, "*.csproj", SearchOption.AllDirectories)) 
                Instance.ProjectFiles.Add(path);
        
        Instance.ReloadTargets();
    }


    public void UpdateSolution(Solution? solution) {
        Solution = solution;
    }
    public void UpdateFramework(string? framework) {
        if (targetFramework == framework) 
            return;
        targetFramework = framework;
        ReloadTargets();
    }


    public void AddTargets(string[] targets) {
        foreach (var target in targets)
            foreach (var path in Directory.GetFiles(target, "*.csproj", SearchOption.AllDirectories)) 
                if (ProjectFiles.Add(path)) 
                    LoadProject(path);

        UpdateSolution(Workspace?.CurrentSolution);
    }
    public void RemoveTargets(string[] targets) {
        bool changed = false;
        foreach (var target in targets) 
            foreach (var path in Directory.GetFiles(target, "*.csproj", SearchOption.AllDirectories))
                changed = ProjectFiles.Remove(path);

        if (changed) 
            ReloadTargets();
    }
    public void ReloadTargets() {
        var configuration = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(targetFramework))
            configuration.Add("TargetFramework", targetFramework);

        Workspace = MSBuildWorkspace.Create(configuration);
        Workspace.LoadMetadataForReferencedProjects = true;
        Workspace.SkipUnrecognizedProjects = true;

        foreach (var path in ProjectFiles) 
            LoadProject(path);

        UpdateSolution(Workspace.CurrentSolution);
    }


    private void LoadProject(string path) {
        try {
            RestoreProject(path);
            Workspace!.OpenProjectAsync(path).Wait();
            LoggingService.Instance.LogMessage("Loaded project {0}", path);
        } catch(Exception ex) {
            LoggingService.Instance.LogError(ex.Message, ex);
        }
    }
    private void RestoreProject(string path) {
        var directory = Path.GetDirectoryName(path);
        if (File.Exists(Path.Combine(directory!, "obj", "project.assets.json")))
            return;

        var result = new ProcessRunner("dotnet", new ProcessArgumentBuilder()
            .Append("restore")
            .AppendQuoted(path))
            .WaitForExit();

        LoggingService.Instance.LogMessage("Restored project {0}", path);
    }
}