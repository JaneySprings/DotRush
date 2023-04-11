using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using dotRush.Server.Processes;

namespace dotRush.Server.Services;

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
        Instance.Workspace = MSBuildWorkspace.Create();
        Instance.Workspace.LoadMetadataForReferencedProjects = true;
        Instance.Workspace.SkipUnrecognizedProjects = true;
        Instance.AddTargets(targets);
    }


    public void UpdateSolution(Solution solution) {
        Solution = solution;
    }
    public void UpdateFramework(string? framework) {
        if (targetFramework == framework) 
            return;
        targetFramework = framework;
        ReloadAll();
    }
    public void AddTargets(string[] targets) {
        foreach (var target in targets)
            foreach (var path in Directory.GetFiles(target, "*.csproj", SearchOption.AllDirectories)) 
                if (ProjectFiles.Add(path)) 
                    LoadProject(path);
    }
    public void RemoveTargets(string[] targets) {
        bool changed = false;
        foreach (var target in targets) 
            foreach (var path in Directory.GetFiles(target, "*.csproj", SearchOption.AllDirectories))
                changed = ProjectFiles.Remove(path);

        if (changed) 
            ReloadAll();
    }


    private void ReloadAll() {
        var configuration = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(targetFramework))
            configuration.Add("TargetFramework", targetFramework);

        Workspace = MSBuildWorkspace.Create(configuration);
        Workspace.LoadMetadataForReferencedProjects = true;
        Workspace.SkipUnrecognizedProjects = true;

        foreach (var path in ProjectFiles) 
            LoadProject(path);
    }
    private void LoadProject(string path) {
        try {
            RestoreProject(path);
            Workspace!.OpenProjectAsync(path).Wait();
            Solution = Workspace!.CurrentSolution;
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