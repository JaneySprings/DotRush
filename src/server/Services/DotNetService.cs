
using System.Diagnostics;

public class DotNetService {
    public static DotNetService Instance { get; } = new DotNetService();

    public async Task RestoreProjectAsync(string projectFilePath, CancellationToken cancellationToken = default) {
        if (!File.Exists(projectFilePath))
            throw new FileNotFoundException("Project file not found for restore!", projectFilePath);

        await StartProcess("dotnet", $"restore \"{projectFilePath}\"", cancellationToken);
    }

    public async Task StartProcess(string command, string args, CancellationToken cancellationToken) {
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
        await process.WaitForExitAsync(cancellationToken);
    }
}