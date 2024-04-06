using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis.MSBuild;
using WorkspaceDiagnostic = DotRush.Roslyn.Workspaces.Models.WorkspaceDiagnostic;

namespace DotRush.Roslyn.Workspaces;

public abstract class ProjectsController {
    private readonly HashSet<string> projectFilePaths = new HashSet<string>();

    public virtual void OnLoadingStarted() {}
    public virtual void OnLoadingCompleted() {}
    public virtual void OnProjectRestoreStarted(string documentPath) {}
    public virtual void OnProjectRestoreCompleted(string documentPath) {}
    public virtual void OnProjectLoadStarted(string documentPath) {}
    public virtual void OnProjectLoadCompleted(string documentPath) {}
    public virtual void OnProjectCompilationStarted(string documentPath) {}
    public virtual void OnProjectCompilationCompleted(string documentPath) {}
    public virtual void OnDiagnosticsReceived(string documentPath, IEnumerable<WorkspaceDiagnostic> diagnostics) {}
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
        OnLoadingStarted();

        foreach (var projectFile in projectFilePaths) {
            var diagnostics = new List<WorkspaceDiagnostic>();
            await SafeExtensions.InvokeAsync(async () => {
                OnProjectRestoreStarted(projectFile);
                var result = await workspace.RestoreProjectAsync(projectFile, cancellationToken);
                diagnostics.Add(result);
                OnProjectRestoreCompleted(projectFile);
                
                OnProjectLoadStarted(projectFile);
                var project = await workspace.OpenProjectAsync(projectFile, null, cancellationToken);
                //TODO: get all diags and filter it by targetSite
                OnProjectLoadCompleted(projectFile);
                
                OnProjectCompilationStarted(projectFile);
                _ = await project.GetCompilationAsync(cancellationToken);
                OnProjectCompilationCompleted(projectFile);
            });

            OnWorkspaceStateChanged(workspace);
            OnDiagnosticsReceived(projectFile, diagnostics);
        }

        OnLoadingCompleted();
        CurrentSessionLogger.Debug($"Projects loading completed, loaded {workspace.CurrentSolution.ProjectIds.Count} projects");
    }
}
