using System.Diagnostics;
using DotRush.Server.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace DotRush.Server.Services;

public class ProjectService {
    public HashSet<string> Projects { get; }
    public Action<Solution?>? WorkspaceUpdated { get; set; }
    public MSBuildWorkspace? Workspace { get; set; }

    private CancellationTokenSource? reloadCancellationTokenSource;
    private CancellationToken CancellationToken {
        get {
            this.reloadCancellationTokenSource?.Cancel();
            this.reloadCancellationTokenSource?.Dispose();
            this.reloadCancellationTokenSource = new CancellationTokenSource();
            return this.reloadCancellationTokenSource.Token;
        }
    }

    public ProjectService() {
        Projects = new HashSet<string>();
    }

    public async Task LoadAsync(IWorkDoneObserver? observer = null, bool forceRestore = false) {
        var cancellationToken = CancellationToken;
        foreach (var projectFile in Projects) {
            await ServerExtensions.SafeHandlerAsync(async () => {
                observer?.OnNext(new WorkDoneProgressReport { Message = $"Loading {Path.GetFileNameWithoutExtension(projectFile)}" });
                if (Workspace.ContainsProjectsWithPath(projectFile))
                    return;

                await RestoreProjectAsync(projectFile, forceRestore, cancellationToken);
                await Workspace!.OpenProjectAsync(projectFile, null, cancellationToken);
                WorkspaceUpdated?.Invoke(Workspace.CurrentSolution);
            });
        }

        observer?.OnCompleted();
    }
    public async Task ReloadAsync(IWorkDoneObserver? observer = null, bool forceRestore = false) {
        WorkspaceUpdated?.Invoke(null);
        Workspace!.CloseSolution();
        await LoadAsync(observer, forceRestore);
    }

    public async Task RestoreProjectAsync(string projectFilePath, bool forceRestore = false, CancellationToken cancellationToken = default) {
        if (!File.Exists(projectFilePath))
            throw new FileNotFoundException("Project file not found for restore!", projectFilePath);

        var projectDirectory = Path.GetDirectoryName(projectFilePath)!;
        if (File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")) && !forceRestore)
            return;

        await StartProcessAsync("dotnet", $"restore \"{projectFilePath}\"", cancellationToken).ConfigureAwait(false);
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