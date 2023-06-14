
using System.Diagnostics;

public class DotNetService {
    public static DotNetService Instance { get; } = new DotNetService();

    public async Task RestoreProjectAsync(string projectFilePath) {
        if (!File.Exists(projectFilePath))
            throw new FileNotFoundException("Project file not found for restore!", projectFilePath);

        var projectDirectory = Path.GetDirectoryName(projectFilePath)!;
        if (File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")))
            return;

        await StartProcess("dotnet", "restore", $"\"{projectFilePath}\"");
    }

    public async Task StartProcess(string command, params string[] args) {
        var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = command,
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            }
        };

        process.Start();
        await process.WaitForExitAsync();
    }
}