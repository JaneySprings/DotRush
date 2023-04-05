using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using dotRush.Server.Processes;

namespace dotRush.Server.Services;

public class SolutionService {
    public static SolutionService? Instance { get; private set; }
    public Solution? Solution { get; private set; }
    public List<string>? Projects { get; private set; }

    private SolutionService() {}

    public static async Task Initialize(string framework, string[] targets) {
        Instance = new SolutionService();
        Instance.Projects = GetAllProjects(targets);

        var queryOptions = VisualStudioInstanceQueryOptions.Default;
        var instances = MSBuildLocator.QueryVisualStudioInstances(queryOptions);
        MSBuildLocator.RegisterInstance(instances.FirstOrDefault());

        var configuration = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(framework) && framework != "-") {
            configuration.Add("TargetFramework", framework);
        }

        var workspace = MSBuildWorkspace.Create(configuration);
        workspace.LoadMetadataForReferencedProjects = true;
        workspace.SkipUnrecognizedProjects = true;
        foreach (var project in Instance.Projects) {
            try {
                RestoreProject(project);
                await workspace.OpenProjectAsync(project);
                LoggingService.Instance?.LogMessage("Loaded project {0}", project);
            } catch(Exception ex) {
                LoggingService.Instance?.LogError(ex.Message, ex);
            }
        }

        Instance.Solution = workspace.CurrentSolution;
    }

    public void UpdateSolution(Solution solution) {
        Solution = solution;
    }


    private static void RestoreProject(string path) {
        var directory = Path.GetDirectoryName(path);
        if (File.Exists(Path.Combine(directory!, "obj", "project.assets.json")))
            return;

        var result = new ProcessRunner("dotnet", new ProcessArgumentBuilder()
            .Append("restore")
            .AppendQuoted(path))
            .WaitForExit();

        LoggingService.Instance?.LogMessage("Restored project {0}", path);
    }
    private static List<string> GetAllProjects(string[] targets) {
        var projects = new List<string>();
        foreach (var target in targets) {
            projects.AddRange(Directory.GetFiles(target, "*.csproj", SearchOption.AllDirectories));
        }
        return projects;
    }
}