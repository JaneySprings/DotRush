using System.Diagnostics;
using System.Text.RegularExpressions;
using DotRush.Server.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace DotRush.Server.Services;

public abstract class ProjectService {
    private CancellationTokenSource? reloadCancellationTokenSource;
    private readonly HashSet<string> projectsPaths;

    protected ProjectService() {
        projectsPaths = new HashSet<string>();
    }

    protected abstract void ProjectFailed(object? sender, WorkspaceDiagnosticEventArgs e);
    protected abstract void RestoreFailed(string message);


    protected void AddProjects(IEnumerable<string> projectsPaths) {
        foreach (var projectPath in projectsPaths)
            this.projectsPaths.Add(projectPath);
    }
    protected void RemoveProjects(IEnumerable<string> projectsPaths) {
        foreach (var projectPath in projectsPaths)
            this.projectsPaths.Remove(projectPath);
    }

    protected async Task LoadAsync(MSBuildWorkspace workspace, Action<Solution?> solutionChanged) {
        CancelReloading();
        workspace.WorkspaceFailed -= ProjectFailed;
        workspace.WorkspaceFailed += ProjectFailed;

        var observer = await LanguageServer.CreateWorkDoneObserverAsync();
        var cancellationToken = reloadCancellationTokenSource?.Token ?? CancellationToken.None;
        foreach (var projectFile in projectsPaths) {
            await ServerExtensions.SafeHandlerAsync(async () => {
                if (workspace.ContainsProjectsWithPath(projectFile))
                    return;

                await RestoreProjectAsync(projectFile, observer, cancellationToken);
                await workspace.OpenProjectAsync(projectFile, new Progress(observer), cancellationToken);
                solutionChanged?.Invoke(workspace.CurrentSolution);
            });
            if (cancellationToken.IsCancellationRequested)
                break;
        }

        observer?.OnCompleted();
        observer?.Dispose();
    }
    protected async Task ReloadAsync(MSBuildWorkspace workspace, Action<Solution?> solutionChanged) {
        solutionChanged.Invoke(null);
        workspace.CloseSolution();
        await LoadAsync(workspace, solutionChanged);
    }

    protected void CancelReloading() {
        if (reloadCancellationTokenSource != null) {
            reloadCancellationTokenSource.Cancel();
            reloadCancellationTokenSource.Dispose();
        }
        reloadCancellationTokenSource = new CancellationTokenSource();
    }


    private async Task RestoreProjectAsync(string projectPath, IWorkDoneObserver? observer, CancellationToken cancellationToken) {
        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = $"restore \"{projectPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            }
        };

        observer?.OnNext(new WorkDoneProgressReport { Message = $"Restoring {projectName}" });
        process.Start();

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            RestoreFailed($"Failed to restore {projectName}. Error code {process.ExitCode}.");
    }

    

    private class Progress: IProgress<ProjectLoadProgress> {
        private IWorkDoneObserver? progressObserver;

        public Progress(IWorkDoneObserver? progressObserver) {
            this.progressObserver = progressObserver;
        }

        void IProgress<ProjectLoadProgress>.Report(ProjectLoadProgress value) {
            var projectName = Path.GetFileNameWithoutExtension(value.FilePath);
            progressObserver?.OnNext(new WorkDoneProgressReport { Message = $"Indexing {projectName}"});
        }
    }
}