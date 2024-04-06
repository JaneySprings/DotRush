using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Protocol = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Services;

public abstract class ProjectService {
    private readonly HashSet<string> projectFilePaths;

    protected ProjectService() {
        projectFilePaths = new HashSet<string>();
    }

    protected abstract void ClearDiagnostics();
    protected abstract void PushDiagnostics(string projectFilePath);
    protected abstract void ProjectDiagnosticReceived(Protocol.Diagnostic diagnostic);


    public void AddProjectFiles(IEnumerable<string> projectPaths) {
        foreach (var projectPath in projectPaths)
            projectFilePaths.Add(projectPath);
    }
    public void RemoveProjectFiles(IEnumerable<string> projectPaths) {
        foreach (var projectPath in projectPaths)
            projectFilePaths.Remove(projectPath);
    }

    protected async Task LoadAsync(MSBuildWorkspace workspace, Action<Solution?> solutionChanged) {
        SessionLogger.LogDebug("Projects loading started");
        var observer = await LanguageServer.CreateWorkDoneObserverAsync();
        var progressObserver = new ProgressObserver(observer);

        foreach (var projectFile in projectFilePaths) {
            ClearDiagnostics();
            await ServerExtensions.SafeHandlerAsync(async () => {
                await workspace.RestoreProjectAsync(projectFile, ProjectDiagnosticReceived, observer, CancellationToken.None);
                var project = await workspace.OpenProjectAsync(projectFile, progressObserver, CancellationToken.None);
                await project.CompileAsync(observer, CancellationToken.None);
            });

            solutionChanged?.Invoke(workspace.CurrentSolution);
            PushDiagnostics(projectFile);
        }

        observer?.OnCompleted();
        observer?.Dispose();
        SessionLogger.LogDebug($"Projects loading completed, loaded {workspace.CurrentSolution.ProjectIds.Count} projects");
    }
}