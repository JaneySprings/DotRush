using DotRush.Server.Processes;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotRush.Server.Services;

public class SolutionService {
    public MSBuildWorkspace? Workspace { get; private set; }
    public Solution? Solution { get; private set; }
    private HashSet<string> ProjectFiles { get; }
    private bool isReloaded;


    public SolutionService(string[] targets) {
        ProjectFiles = new HashSet<string>();
        MSBuildLocator.RegisterDefaults();

        foreach (var target in targets) 
            AddProjects(Directory.GetFiles(target, "*.csproj", SearchOption.AllDirectories));

        foreach (var projectPath in ProjectFiles)
            RestoreProject(projectPath);
    }


    public void UpdateSolution(Solution solution) {
        Solution = solution;
    }
    public async Task ReloadSolution(CancellationToken cancellationToken) {
        if (isReloaded)
            return;
        
        isReloaded = true;
        Solution = null;
        if (Workspace != null) {
            Workspace.Dispose();
            Workspace = null;
        }
        Workspace = MSBuildWorkspace.Create();
        Workspace.LoadMetadataForReferencedProjects = true;
        Workspace.SkipUnrecognizedProjects = true;
        await LoadProjects(cancellationToken);
        isReloaded = false;
    }
    public void AddProjects(IEnumerable<string> projectFilePaths) {
        foreach (var path in projectFilePaths)
            ProjectFiles.Add(path);
    }
    public void RemoveProjects(IEnumerable<string> projectFilePaths) {
        foreach (var path in projectFilePaths)
            ProjectFiles.Remove(path);
    }
    
    
    private async Task LoadProjects(CancellationToken cancellationToken) {
        foreach (var path in ProjectFiles) {
            try {
                var project = await Workspace!.OpenProjectAsync(path, cancellationToken: cancellationToken);
                if (project == null)
                    continue;
                
                UpdateSolution(Workspace.CurrentSolution);
            } catch(Exception ex) {
                LoggingService.Instance.LogError(ex.Message, ex);
            }
        }
    }
    private void RestoreProject(string path) {
        var directory = Path.GetDirectoryName(path);
        if (directory == null)
            return;

        if (Directory.Exists(Path.Combine(directory, "obj")))
            Directory.Delete(Path.Combine(directory, "obj"), true);

        if (Directory.Exists(Path.Combine(directory, "bin")))
            Directory.Delete(Path.Combine(directory, "bin"), true);

        var result = new ProcessRunner("dotnet", new ProcessArgumentBuilder()
            .Append("restore")
            .AppendQuoted(path))
            .WaitForExit();

        LoggingService.Instance.LogMessage("Restored project {0}", path);
    }
}