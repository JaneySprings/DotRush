using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotRush.Roslyn.Workspaces;

public abstract class ProjectsController {
    private readonly HashSet<string> projectFilePaths = new HashSet<string>();

    public virtual Task OnLoadingStartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual Task OnLoadingCompletedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual void OnProjectRestoreStarted(string documentPath) {}
    public virtual void OnProjectRestoreCompleted(string documentPath) {}
    public virtual void OnProjectRestoreFailed(string documentPath, int exitCode) {}
    public virtual void OnProjectLoadStarted(string documentPath) {}
    public virtual void OnProjectLoadCompleted(string documentPath) {}
    public virtual void OnProjectCompilationStarted(string documentPath) {}
    public virtual void OnProjectCompilationCompleted(string documentPath) {}
    protected abstract void OnWorkspaceStateChanged(MSBuildWorkspace workspace);

    public static bool IsSourceCodeDocument(string filePath) {
        var allowedExtensions = new[] { ".cs", /* .fs .vb */};
        return allowedExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsAdditionalDocument(string filePath) {
        var allowedExtensions = new[] { ".xaml", /* maybe '.razor' ? */};
        return allowedExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsProjectFile(string filePath) {
        var allowedExtensions = new[] { ".csproj", /* fsproj vbproj */};
        return allowedExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }

    protected void AddProjectFiles(IEnumerable<string> projectPaths) {
        foreach (var projectPath in projectPaths)
            projectFilePaths.Add(projectPath);
    }
    protected void RemoveProjectFiles(IEnumerable<string> projectPaths) {
        foreach (var projectPath in projectPaths)
            projectFilePaths.Remove(projectPath);
    }
    protected void ClearAllProjects() {
        projectFilePaths.Clear();
    }
    protected async Task LoadAsync(MSBuildWorkspace workspace, CancellationToken cancellationToken) {
        CurrentSessionLogger.Debug($"Loading projects: {string.Join(';', projectFilePaths)}");
        await OnLoadingStartedAsync(cancellationToken);

        foreach (var projectFile in projectFilePaths) {
            await SafeExtensions.InvokeAsync(async () => {
                OnProjectRestoreStarted(projectFile);
                var result = await workspace.RestoreProjectAsync(projectFile, cancellationToken);
                if (result.ExitCode != 0)
                    OnProjectRestoreFailed(projectFile, result.ExitCode);
                OnProjectRestoreCompleted(projectFile);
                
                OnProjectLoadStarted(projectFile);
                var project = await workspace.OpenProjectAsync(projectFile, null, cancellationToken);
                OnProjectLoadCompleted(projectFile);

                OnProjectCompilationStarted(projectFile);
                _ = await project.GetCompilationAsync(cancellationToken);
                OnProjectCompilationCompleted(projectFile);
            });

            OnWorkspaceStateChanged(workspace);
        }

        await OnLoadingCompletedAsync(cancellationToken);
        CurrentSessionLogger.Debug($"Projects loading completed, loaded {workspace.CurrentSolution.ProjectIds.Count} projects");
    }
}
