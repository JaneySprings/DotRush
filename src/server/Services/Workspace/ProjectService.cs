using System.Diagnostics;
using System.Text;
using DotRush.Server.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace DotRush.Server.Services;

public abstract class ProjectService {
    protected WorkspaceNotifications Notifications { get; private set; }
    protected HashSet<string> ProjectsPaths { get; private set; }
    protected Action<Solution?>? WorkspaceUpdated { get; set; }
    protected MSBuildWorkspace? Workspace { get; set; }

    private CancellationTokenSource? reloadCancellationTokenSource;
    private CancellationToken CancellationToken {
        get {
            CancelWorkspaceReloading();
            reloadCancellationTokenSource = new CancellationTokenSource();
            return reloadCancellationTokenSource.Token;
        }
    }

    protected ProjectService() {
        Notifications = new WorkspaceNotifications();
        ProjectsPaths = new HashSet<string>();
    }

    protected void AddProjects(IEnumerable<string> projectsPaths) {
        var projectGroups = projectsPaths.GroupBy(p => Path.GetDirectoryName(p));
        foreach (var group in projectGroups) {
            ProjectsPaths.Add(group
                .OrderBy(p => Path.GetFileNameWithoutExtension(p).Length)
                .First());
        }
    }
    protected void RemoveProjects(IEnumerable<string> projectsPaths) {
        foreach (var projectPath in projectsPaths)
            ProjectsPaths.Remove(projectPath);
    }

    protected async Task LoadAsync(IWorkDoneObserver? observer = null) {
        Notifications.SetWorkDoneObserver(observer);
        var cancellationToken = CancellationToken;
        foreach (var projectFile in ProjectsPaths) {
            if (cancellationToken.IsCancellationRequested)
                break;

            await ServerExtensions.SafeHandlerAsync(async () => {
                if (Workspace.ContainsProjectsWithPath(projectFile))
                    return;

                observer?.OnNext(new WorkDoneProgressReport { Message = $"Restoring {Path.GetFileNameWithoutExtension(projectFile)}" });
                await StartProcessAsync("dotnet", $"restore \"{projectFile}\"", cancellationToken);
                await Workspace!.OpenProjectAsync(projectFile, Notifications, cancellationToken);
                WorkspaceUpdated?.Invoke(Workspace.CurrentSolution);
            });
        }

        observer?.OnCompleted();
    }
    protected async Task ReloadAsync(IWorkDoneObserver? observer = null) {
        Notifications.ResetWorkspaceErrors();
        WorkspaceUpdated?.Invoke(null);
        Workspace!.CloseSolution();
        await LoadAsync(observer);
    }

    protected void CancelWorkspaceReloading() {
        if (this.reloadCancellationTokenSource == null)
            return;

        this.reloadCancellationTokenSource.Cancel();
        this.reloadCancellationTokenSource.Dispose();
        this.reloadCancellationTokenSource = null;
    }

    private async Task StartProcessAsync(string command, string args, CancellationToken cancellationToken) {
        var errorMessage = new StringBuilder();
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

        process.ErrorDataReceived += (sender, e) => {
            if (!string.IsNullOrEmpty(e.Data))
                errorMessage.AppendLine(e.Data);
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        Notifications.NotifyWorkspaceFailed(errorMessage.ToString());
    }
}