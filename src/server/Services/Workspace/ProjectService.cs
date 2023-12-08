using DotRush.Server.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Protocol = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Services;

public abstract class ProjectService {
    private CancellationTokenSource? reloadCancellationTokenSource;
    private readonly HashSet<string> projectFilePaths;

    protected ProjectService() {
        projectFilePaths = new HashSet<string>();
    }

    protected abstract void ClearDiagnostics();
    protected abstract void PushDiagnostics(string projectFilePath);
    protected abstract void ProjectDiagnosticReceived(Protocol.Diagnostic diagnostic);


    protected void AddProjects(IEnumerable<string> projectsPaths) {
        foreach (var projectPath in projectsPaths)
            this.projectFilePaths.Add(projectPath);
    }
    protected void RemoveProjects(IEnumerable<string> projectsPaths) {
        foreach (var projectPath in projectsPaths)
            this.projectFilePaths.Remove(projectPath);
    }

    protected async Task LoadAsync(MSBuildWorkspace workspace, Action<Solution?> solutionChanged) {
        if (reloadCancellationTokenSource != null) {
            reloadCancellationTokenSource.Cancel();
            reloadCancellationTokenSource.Dispose();
        }
    
        reloadCancellationTokenSource = new CancellationTokenSource();

        var observer = await LanguageServer.CreateWorkDoneObserverAsync();
        var progressObserver = new ProgressObserver(Resources.MessageProjectIndex, observer);
        var cancellationToken = reloadCancellationTokenSource.Token;

        foreach (var projectFile in projectFilePaths) {
            ClearDiagnostics();

            if (cancellationToken.IsCancellationRequested)
                break;
            if (workspace.ContainsProjectsWithPath(projectFile))
                continue;

            await ServerExtensions.SafeHandlerAsync(async () => {
                await workspace.RestoreProjectAsync(projectFile, ProjectDiagnosticReceived, observer, cancellationToken);
                await workspace.OpenProjectAsync(projectFile, progressObserver, cancellationToken);
            });

            solutionChanged?.Invoke(workspace.CurrentSolution);
            PushDiagnostics(projectFile);
        }

        solutionChanged?.Invoke(RemoveDuplicateProjects(workspace.CurrentSolution));
        observer?.OnCompleted();
        observer?.Dispose();
    }

    public Solution RemoveDuplicateProjects(Solution solution) {
        var records = new List<string>();
        foreach (var project in solution.Projects) {
            var record = $"{project.Name} {project.FilePath}";
            if (records.FirstOrDefault(x => x.Equals(record, StringComparison.OrdinalIgnoreCase)) != null) {
                solution = solution.RemoveProject(project.Id);
                continue;
            }
            records.Add(record);
        }

        return solution;
    }
}