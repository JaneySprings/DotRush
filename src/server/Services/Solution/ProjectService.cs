using System.Diagnostics;
using DotRush.Server.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace DotRush.Server.Services;

public abstract class ProjectService {
    protected Action<Solution?>? WorkspaceUpdated { get; set; }
    protected MSBuildWorkspace? Workspace { get; set; }

    protected HashSet<string> projects;

    private CancellationTokenSource? reloadCancellationTokenSource;
    private CancellationToken CancellationToken {
        get {
            CancelWorkspaceReloading();
            this.reloadCancellationTokenSource = new CancellationTokenSource();
            return this.reloadCancellationTokenSource.Token;
        }
    }

    protected ProjectService() {
        this.projects = new HashSet<string>();
    }

    protected void AddProjects(IEnumerable<string> projects) {
        var projectGroups = projects.GroupBy(p => Path.GetDirectoryName(p));
        foreach (var group in projectGroups) {
            var orderedGroup = group
                .OrderByDescending(p => p.Length)
                .ThenByDescending(p => Path.GetFileNameWithoutExtension(p));

            this.projects.Add(orderedGroup.First());
        }
    }
    protected void RemoveProjects(IEnumerable<string> projects) {
        foreach (var project in projects)
            this.projects.Remove(project);
    }

    protected async Task LoadAsync(IWorkDoneObserver? observer = null) {
        var cancellationToken = CancellationToken;
        foreach (var projectFile in this.projects) {
            if (cancellationToken.IsCancellationRequested)
                break;

            await ServerExtensions.SafeHandlerAsync(async () => {
                if (Workspace.ContainsProjectsWithPath(projectFile))
                    return;

                observer?.OnNext(new WorkDoneProgressReport { Message = $"Restoring {Path.GetFileNameWithoutExtension(projectFile)}" });
                await RestoreProjectAsync(projectFile, cancellationToken);
                await Workspace!.OpenProjectAsync(projectFile, new ProgressNotification(observer), cancellationToken);
                WorkspaceUpdated?.Invoke(Workspace.CurrentSolution);
            });
        }

        observer?.OnCompleted();
    }
    protected async Task ReloadAsync(IWorkDoneObserver? observer = null) {
        WorkspaceUpdated?.Invoke(null);
        Workspace!.CloseSolution();
        await LoadAsync(observer);
    }

    protected async Task RestoreProjectAsync(string projectFilePath, CancellationToken cancellationToken = default) {
        if (!File.Exists(projectFilePath))
            throw new FileNotFoundException("Project file not found for restore!", projectFilePath);

        await StartProcessAsync("dotnet", $"restore \"{projectFilePath}\"", cancellationToken).ConfigureAwait(false);
    }

    protected void CancelWorkspaceReloading() {
        if (this.reloadCancellationTokenSource == null)
            return;

        this.reloadCancellationTokenSource.Cancel();
        this.reloadCancellationTokenSource.Dispose();
        this.reloadCancellationTokenSource = null;
    }

    private static async Task StartProcessAsync(string command, string args, CancellationToken cancellationToken) {
        var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = command,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }
}