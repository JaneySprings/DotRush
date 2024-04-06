using System.Diagnostics;
using System.Runtime.InteropServices;
using DotRush.Roslyn.Workspaces.Models;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotRush.Roslyn.Workspaces.Extensions;

public static class NugetExtensions {
    public static async Task<WorkspaceDiagnostic> RestoreProjectAsync(this MSBuildWorkspace workspace, string projectPath, CancellationToken cancellationToken) {
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

        process.Start();
        await process.WaitForExitAsync(cancellationToken);
        var exitCode = process.ExitCode;
        process.Close();

        var diagnostics = new List<WorkspaceDiagnostic>();
        if (exitCode == 0)
            return diagnostics;

        return diagnostics;
    }
}
