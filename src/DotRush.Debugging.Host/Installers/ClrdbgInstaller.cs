using System.IO.Compression;
using System.Security.Cryptography;
using DotRush.Common;
using DotRush.Common.Extensions;
using DotRush.Common.InteropV2;
using DotRush.Common.Logging;

namespace DotRush.Debugging.Host.Installers;

public class ClrdbgInstaller : IDebuggerInstaller {
    private const string LatestReleaseVersion = "18.1.0";
    private readonly string debuggerDirectory;
    private readonly string runtime;
    private readonly Dictionary<string, string> releaseChecksums;

    public ClrdbgInstaller(string workingDirectory) {
        debuggerDirectory = Path.Combine(workingDirectory, "Debugger");
        runtime = $"{RuntimeInfo.GetOperationSystemV2()}-{RuntimeInfo.GetArchitecture64()}";
        releaseChecksums = new Dictionary<string, string>() {
            { "linux-arm64", "51de325acb5ec59d890f3ff88fe77e82650e188acd142118304b1941333a9fb3" },
            { "linux-x64", "767e07f87a9c439b82649ee219bd3c741c4a38832cdd4710db94d3f17a249b91" },
            { "osx-arm64", "d39fd396bdc6a217bb55e4cbecb4dd802f788072a8c23f5028c7d5ca12b64eed" },
            { "osx-x64", "92e31f6690949d9c4975d56ae64603d21738ced28a1619716958be2d592f0e0d" },
            { "win-arm64", "59530a875b1c7efb8c2211c454fed7587f1c935a8f472a0c04268ed4ac4ee36f" },
            { "win-x64", "9b98acaf7cd59f6b2eeb3e9ebdd812c52986c79f30d10d75b8bf46c9286e4e53" }
        };
    }

    void IDebuggerInstaller.BeginInstallation() {
        FileSystemExtensions.TryDeleteDirectory(debuggerDirectory);
    }
    string IDebuggerInstaller.GetDownloadLink() {
        if (!releaseChecksums.ContainsKey(runtime))
            throw new PlatformNotSupportedException($"Debugger is not available for '{runtime}'");
        return $"https://github.com/JaneySprings/clrdbg/releases/download/{LatestReleaseVersion}/clrdbg_{runtime}.zip";
    }
    string IDebuggerInstaller.Install(string downloadUrl) {
        CurrentSessionLogger.Debug($"Downloading debugger from '{downloadUrl}'");

        using var httpClient = new HttpClient();
        var response = httpClient.GetAsync(downloadUrl).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Failed to download debugger: {response.StatusCode}");

        using var memoryStream = new MemoryStream();
        response.Content.ReadAsStream().CopyTo(memoryStream);
        memoryStream.Position = 0;

        var checksum = Convert.ToHexStringLower(SHA256.HashData(memoryStream));
        if (checksum != releaseChecksums[runtime])
            throw new InvalidOperationException($"Debugger checksum mismatch: expected '{releaseChecksums[runtime]}', actual '{checksum}'");

        CurrentSessionLogger.Debug($"Extracting debugger to '{debuggerDirectory}'");
        memoryStream.Position = 0;
        ZipFile.ExtractToDirectory(memoryStream, debuggerDirectory);

        var executable = Path.Combine(debuggerDirectory, "clrdbg" + RuntimeInfo.ExecExtension);
        if (!File.Exists(executable))
            throw new FileNotFoundException($"Debugger executable not found: '{executable}'");

        return executable;
    }
    void IDebuggerInstaller.EndInstallation(string executablePath) {
        if (!RuntimeInfo.IsWindows) {
            var registrationResult = ProcessRunner.CreateProcess("chmod", $"+x \"{executablePath}\"", captureOutput: true, displayWindow: false).Task.Result;
            if (!registrationResult.Success)
                throw new InvalidOperationException($"Failed to register debugger executable: {registrationResult.GetError()}");
        }
    }
}
