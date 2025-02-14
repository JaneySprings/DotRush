using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotRush.Essentials.Common;
using DotRush.Essentials.Common.External;
using DotRush.Essentials.Common.Logging;

namespace DotRush.Essentials.Workspaces.Debugger;

public static class VsdbgDownloader {
    //vsDbgUrl = "https://aka.ms/getvsdbgsh";
    private const string OmnisharpPackageConfigUrl = "https://raw.githubusercontent.com/dotnet/vscode-csharp/main/package.json";
    private const string OmnisharpDebuggerId = "Debugger";

    public static async Task<string?> ObtainDebuggerLinkAsync() {
        CurrentSessionLogger.Debug($"Obtaining vsdbg via link: {OmnisharpPackageConfigUrl}");

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(OmnisharpPackageConfigUrl);
        if (!response.IsSuccessStatusCode) {
            CurrentSessionLogger.Error($"Failed to fetch omnisharp package config: {response.StatusCode}");
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        var packageModel = JsonSerializer.Deserialize<PackageModel>(content);
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
    public static async Task<string?> InstallDebuggerAsync(string downloadUrl) {
        var workingDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
        var downloadPath = Path.Combine(workingDirectory, "vsdbg.zip");
        var extractPath = Path.Combine(workingDirectory, "Debugger");

        CurrentSessionLogger.Debug($"Downloading debugger from '{downloadUrl}'");

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(downloadUrl);
        if (!response.IsSuccessStatusCode) {
            CurrentSessionLogger.Error($"Failed to download debugger: {response.StatusCode}");
            return null;
        }

        CurrentSessionLogger.Debug($"Extracting debugger to '{extractPath}'");

        using var archive = new ZipArchive(response.Content.ReadAsStream());
        foreach (var entry in archive.Entries) {
            var targetPath = Path.Combine(extractPath, entry.FullName);
            var targetDirectory = Path.GetDirectoryName(targetPath)!;

            if (string.IsNullOrEmpty(Path.GetFileName(targetPath)))
                continue;
            if (!Directory.Exists(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            using var fileStream = File.Create(targetPath);
            using var stream = entry.Open();
            stream.CopyTo(fileStream);
        }

        var executable = Path.Combine(extractPath, "vsdbg" + RuntimeInfo.ExecExtension);
        if (!File.Exists(executable)) {
            CurrentSessionLogger.Error($"Debugger executable not found: '{executable}'");
            return null;
        }

        if (!RuntimeInfo.IsWindows) {
            var registrationResult = new ProcessRunner("chmod", new ProcessArgumentBuilder()
                .Append("+x").AppendQuoted(executable)).WaitForExit();
            if (!registrationResult.Success)
                CurrentSessionLogger.Error($"Failed to register debugger executable: {registrationResult.GetError()}");
        }

        return executable;
    }

    private class PackageModel {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("runtimeDependencies")]
        public IEnumerable<RuntimeDependencyModel>? RuntimeDependencies { get; set; }
    }
    private class RuntimeDependencyModel {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("platforms")]
        public IEnumerable<string> Platforms { get; set; } = Enumerable.Empty<string>();

        [JsonPropertyName("architectures")]
        public IEnumerable<string> Architectures { get; set; } = Enumerable.Empty<string>();

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
