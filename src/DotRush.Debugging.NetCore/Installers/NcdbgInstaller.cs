using System.IO.Compression;
using DotRush.Common;
using DotRush.Common.Extensions;
using DotRush.Common.External;
using DotRush.Common.Logging;

namespace DotRush.Debugging.NetCore.Installers;

public class NcdbgInstaller : IDebuggerInstaller {
    private const string LatestReleaseVersion = "3.1.2-1054";
    private readonly string debuggerDirectory;

    public NcdbgInstaller(string workingDirectory) {
        debuggerDirectory = Path.Combine(workingDirectory, "Debugger");
    }

    void IDebuggerInstaller.BeginInstallation() {
        FileSystemExtensions.TryDeleteDirectory(debuggerDirectory);
    }
    string? IDebuggerInstaller.GetDownloadLink() {
        var runtimeExtension = string.Empty;
        if (RuntimeInfo.IsWindows)
            runtimeExtension = "win64.zip";
        if (RuntimeInfo.IsMacOS)
            runtimeExtension = "osx-amd64.tar.gz";
        if (RuntimeInfo.IsLinux)
            runtimeExtension = "linux-amd64.tar.gz";

        return $"https://github.com/Samsung/netcoredbg/releases/download/{LatestReleaseVersion}/netcoredbg-{runtimeExtension}";
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

        var executable = Path.Combine(debuggerDirectory, "netcoredbg" + RuntimeInfo.ExecExtension);
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

        var linkPath = Path.Combine(Path.GetDirectoryName(executablePath)!, "vsdbg" + RuntimeInfo.ExecExtension);
        File.Copy(executablePath, linkPath);
    }
}
