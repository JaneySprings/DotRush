using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using DotRush.Common;
using DotRush.Common.Extensions;
using DotRush.Common.InteropV2;
using DotRush.Common.Logging;
using DotRush.Debugging.NetCore.Models;

namespace DotRush.Debugging.NetCore.Installers;

public class VsdbgInstaller : IDebuggerInstaller {
    //vsDbgUrl = "https://aka.ms/getvsdbgsh";
    private const string OmnisharpPackageConfigUrl = "https://raw.githubusercontent.com/dotnet/vscode-csharp/main/package.json";
    private const string OmnisharpDebuggerId = "Debugger";
    private readonly string debuggerDirectory;

    public VsdbgInstaller(string workingDirectory) {
        debuggerDirectory = Path.Combine(workingDirectory, "Debugger");
    }

    void IDebuggerInstaller.BeginInstallation() {
        FileSystemExtensions.TryDeleteDirectory(debuggerDirectory);
    }
    string? IDebuggerInstaller.GetDownloadLink() {
        CurrentSessionLogger.Debug($"Obtaining vsdbg via link: {OmnisharpPackageConfigUrl}");

        using var httpClient = new HttpClient();
        var response = httpClient.GetAsync(OmnisharpPackageConfigUrl).Result;
        if (!response.IsSuccessStatusCode) {
            CurrentSessionLogger.Error($"Failed to fetch omnisharp package config: {response.StatusCode}");
            return null;
        }

        var content = response.Content.ReadAsStringAsync().Result;
        var packageModel = JsonSerializer.Deserialize<PackageInfo>(content);
        if (packageModel == null) {
            CurrentSessionLogger.Error("Failed to deserialize omnisharp package config");
            return null;
        }

        CurrentSessionLogger.Debug($"Omnisharp package received: {packageModel.Name} - {packageModel.Version}");

        var debuggers = packageModel.RuntimeDependencies?.Where(x => x.Id == OmnisharpDebuggerId);
        if (debuggers == null || !debuggers.Any()) {
            CurrentSessionLogger.Error("No debuggers found in omnisharp package config");
            return null;
        }

        var platform = RuntimeInfo.GetOperationSystem();
        var arch = RuntimeInfo.GetArchitecture();
        var result = debuggers
            .Where(d => d.Platforms.Contains(platform) && d.Architectures.Contains(arch))
            .OrderBy(d => d.Architectures.Count())
            .FirstOrDefault();

        if (result == null) {
            CurrentSessionLogger.Error($"No suitable debugger found for {platform}-{arch}");
            return null;
        }

        return result.Url;
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
            var targetPath = Path.Combine(debuggerDirectory, entry.FullName);
            var targetDirectory = Path.GetDirectoryName(targetPath)!;

            if (string.IsNullOrEmpty(Path.GetFileName(targetPath)))
                continue;
            if (!Directory.Exists(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            using var fileStream = File.Create(targetPath);
            using var stream = entry.Open();
            stream.CopyTo(fileStream);
        }

        var executable = Path.Combine(debuggerDirectory, "vsdbg-ui" + RuntimeInfo.ExecExtension);
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
