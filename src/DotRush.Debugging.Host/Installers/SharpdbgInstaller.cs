using System.IO.Compression;
using DotRush.Common;
using DotRush.Common.Extensions;
using DotRush.Common.InteropV2;
using DotRush.Common.Logging;

namespace DotRush.Debugging.Host.Installers;

public class SharpdbgInstaller : IDebuggerInstaller {
    private const string LatestReleaseVersion = "0.1.10";
    private readonly string debuggerDirectory;

    public SharpdbgInstaller(string workingDirectory) {
        debuggerDirectory = Path.Combine(workingDirectory, "Debugger");
    }

    void IDebuggerInstaller.BeginInstallation() {
        FileSystemExtensions.TryDeleteDirectory(debuggerDirectory);
    }
    string? IDebuggerInstaller.GetDownloadLink() {
        var runtime = $"{RuntimeInfo.GetOperationSystemV2()}-{RuntimeInfo.GetArchitecture64()}";
        return $"https://github.com/JaneySprings/sharpdbg/releases/download/{LatestReleaseVersion}/SharpDbg-{runtime}.zip";
    }
    string? IDebuggerInstaller.Install(string downloadUrl) {
        CurrentSessionLogger.Debug($"Downloading debugger from '{downloadUrl}'");

        using var httpClient = new HttpClient();
        var response = httpClient.GetAsync(downloadUrl).Result;
        if (!response.IsSuccessStatusCode) {
            CurrentSessionLogger.Error($"Failed to download debugger: {response.StatusCode}");
            return null;
        }

        CurrentSessionLogger.Debug($"Extracting debugger to '{debuggerDirectory}'");

        using var archive = new ZipArchive(response.Content.ReadAsStream());
        foreach (var entry in archive.Entries) {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var targetPath = Path.Combine(debuggerDirectory, entry.Name);
            var targetDirectory = Path.GetDirectoryName(targetPath)!;
            if (!Directory.Exists(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            using var fileStream = File.Create(targetPath);
            using var stream = entry.Open();
            stream.CopyTo(fileStream);
        }

        var executable = Path.Combine(debuggerDirectory, "SharpDbg.Cli" + RuntimeInfo.ExecExtension);
        if (!File.Exists(executable)) {
            CurrentSessionLogger.Error($"Debugger executable not found: '{executable}'");
            return null;
        }

        return executable;
    }
    void IDebuggerInstaller.EndInstallation(string executablePath) {
        if (!RuntimeInfo.IsWindows) {
            var registrationResult = ProcessRunner.CreateProcess("chmod", $"+x \"{executablePath}\"", captureOutput: true, displayWindow: false).Task.Result;
            if (!registrationResult.Success)
                CurrentSessionLogger.Error($"Failed to register debugger executable: {registrationResult.GetError()}");
        }

        var linkPath = Path.Combine(Path.GetDirectoryName(executablePath)!, "clrdbg" + RuntimeInfo.ExecExtension);
        File.Copy(executablePath, linkPath);
    }
}
