using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;
using Protocol = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Extensions;

public static class NugetExtensions {
    public static async Task RestoreProjectAsync(this MSBuildWorkspace workspace, string projectPath, Action<Protocol.Diagnostic> errorHandler, IWorkDoneObserver? observer, CancellationToken cancellationToken) {
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

        var options = string.Join(" ", workspace.Properties.Select(x => $"-p:{x.Key}={x.Value}"));
        if (!string.IsNullOrEmpty(options))
            process.StartInfo.Arguments += $" {options}";

        observer?.OnNext(new WorkDoneProgressReport { Message = string.Format(Resources.MessageProjectRestore, projectName) });
        process.Start();

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode == 0) 
            return;

        var message = string.Format(Resources.MessageProjectRestoreFailed, projectName, process.ExitCode);
        errorHandler?.Invoke(CreateDiagnostic(message));
    }

    private static Protocol.Diagnostic CreateDiagnostic(string message) {
        return new Protocol.Diagnostic() {
            Message = message,
            Severity = Protocol.DiagnosticSeverity.Error,
            Range = new Protocol.Range() {
                Start = new Protocol.Position(0, 0),
                End = new Protocol.Position(0, 0)
            },
        };
    }
}