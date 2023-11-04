using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
        if (reloadCancellationTokenSource != null) {
            reloadCancellationTokenSource.Cancel();
            reloadCancellationTokenSource.Dispose();
        }
    
        workspace.WorkspaceFailed -= ProjectFailed;
        workspace.WorkspaceFailed += ProjectFailed;
        reloadCancellationTokenSource = new CancellationTokenSource();

        var observer = await LanguageServer.CreateWorkDoneObserverAsync();
        var cancellationToken = reloadCancellationTokenSource.Token;
        foreach (var projectFile in projectsPaths) {
            await ServerExtensions.SafeHandlerAsync(async () => {
                if (workspace.ContainsProjectsWithPath(projectFile))
                    return;

                await RestoreProjectAsync(projectFile, workspace.Properties, observer, cancellationToken);
                await workspace.OpenProjectAsync(projectFile, new Progress(observer), cancellationToken);
                solutionChanged?.Invoke(workspace.CurrentSolution);
            });
            if (cancellationToken.IsCancellationRequested)
                break;
        }

        observer?.OnCompleted();
        observer?.Dispose();
    }

    private async Task RestoreProjectAsync(string projectPath, ImmutableDictionary<string, string> properties, IWorkDoneObserver? observer, CancellationToken cancellationToken) {
        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = $"restore \"{projectPath}\" --verbosity quiet",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardInput = true;
        }

        var options = string.Join(" ", properties.Select(x => $"-p:{x.Key}={x.Value}"));
        if (!string.IsNullOrEmpty(options))
            process.StartInfo.Arguments += $" {options}";

        observer?.OnNext(new WorkDoneProgressReport { Message = string.Format(Resources.MessageProjectRestore, projectName) });
        process.Start();

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            RestoreFailed(string.Format(Resources.MessageProjectRestoreFailed, projectName, process.ExitCode));
    }


    private class Progress: IProgress<ProjectLoadProgress> {
        private IWorkDoneObserver? progressObserver;

        public Progress(IWorkDoneObserver? progressObserver) {
            this.progressObserver = progressObserver;
        }

        void IProgress<ProjectLoadProgress>.Report(ProjectLoadProgress value) {
            var projectName = Path.GetFileNameWithoutExtension(value.FilePath);
            progressObserver?.OnNext(new WorkDoneProgressReport { Message = string.Format(Resources.MessageProjectIndex, projectName)});
        }
    }
}