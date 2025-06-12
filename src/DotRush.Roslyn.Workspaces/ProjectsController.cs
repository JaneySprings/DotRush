using DotRush.Common.Extensions;
using DotRush.Common.InteropV2;
using DotRush.Common.Logging;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotRush.Roslyn.Workspaces;

public abstract class ProjectsController {

    protected abstract bool RestoreProjectsBeforeLoading { get; }
    protected abstract bool CompileProjectsAfterLoading { get; }

    public virtual Task OnLoadingStartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual Task OnLoadingCompletedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual void OnProjectRestoreStarted(string documentPath) { }
    public virtual void OnProjectRestoreCompleted(string documentPath) { }
    public virtual void OnProjectRestoreFailed(string documentPath, ProcessResult result) { }
    public virtual void OnProjectLoadStarted(string documentPath) { }
    public virtual void OnProjectLoadCompleted(Project project) { }
    public virtual void OnProjectCompilationStarted(string documentPath) { }
    public virtual void OnProjectCompilationCompleted(Project project) { }
    protected abstract void OnWorkspaceStateChanged(Solution newSolution);

    protected async Task LoadProjectsAsync(MSBuildWorkspace workspace, IEnumerable<string> projectFilePaths, CancellationToken cancellationToken) {
        CurrentSessionLogger.Debug($"Loading projects: {string.Join(';', projectFilePaths)}"); ;

        foreach (var projectFile in projectFilePaths) {
            await SafeExtensions.InvokeAsync(async () => {
                if (RestoreProjectsBeforeLoading) {
                    OnProjectRestoreStarted(projectFile);
                    var result = await workspace.RestoreProjectAsync(projectFile, cancellationToken);
                    if (result.ExitCode != 0)
                        OnProjectRestoreFailed(projectFile, result);
                    OnProjectRestoreCompleted(projectFile);
                }

                OnProjectLoadStarted(projectFile);
                var project = await workspace.OpenProjectAsync(projectFile, null, cancellationToken);
                OnProjectLoadCompleted(project);

                OnWorkspaceStateChanged(workspace.CurrentSolution);

                if (CompileProjectsAfterLoading) {
                    OnProjectCompilationStarted(projectFile);
                    _ = await project.GetCompilationAsync(cancellationToken);
                    OnProjectCompilationCompleted(project);
                }
            });
        }

        CurrentSessionLogger.Debug($"Projects loading completed, loaded {workspace.CurrentSolution.ProjectIds.Count} projects");
    }
}
