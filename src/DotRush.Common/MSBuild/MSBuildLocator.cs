using DotRush.Common.Interop;

namespace DotRush.Common.MSBuild;

public static class MSBuildLocator {
    public static string? DotNetSdkDirectory { get; set; }

    public static FileInfo DotNetTool {
        get {
            var path = Path.Combine(MSBuildLocator.GetRootDirectory(), "dotnet" + RuntimeInfo.ExecExtension);
            if (!File.Exists(path))
                throw new FileNotFoundException("Could not find 'dotnet' tool");

            return new FileInfo(path);
        }
    }

    public static string GetRootDirectory() {
        if (!string.IsNullOrEmpty(DotNetSdkDirectory))
            return Path.GetFullPath(Path.Combine(DotNetSdkDirectory, "..", ".."));

        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(dotnetRoot) && Directory.Exists(dotnetRoot))
            return dotnetRoot;

        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath) && processPath.EndsWith("dotnet" + RuntimeInfo.ExecExtension, StringComparison.OrdinalIgnoreCase)) {
            dotnetRoot = Path.GetDirectoryName(processPath);
            if (Directory.Exists(dotnetRoot))
                return dotnetRoot;
        }

        if (RuntimeInfo.IsWindows)
            dotnetRoot = Path.Combine("C:", "Program Files", "dotnet");
        else if (RuntimeInfo.IsMacOS)
            dotnetRoot = Path.Combine("/usr", "local", "share", "dotnet");
        else
            dotnetRoot = Path.Combine("/usr", "share", "dotnet");

        if (Directory.Exists(dotnetRoot))
            return dotnetRoot;

        throw new FileNotFoundException("Could not find dotnet tool");
    }
    public static string GetLatestSdkDirectory() {
        if (!string.IsNullOrEmpty(DotNetSdkDirectory))
            return DotNetSdkDirectory;

        var sdkPath = Path.Combine(GetRootDirectory(), "sdk");
        var result = new ProcessRunner(DotNetTool, new ProcessArgumentBuilder()
           .Append("--version")).WaitForExit();
        if (result.Success)
            return Path.Combine(sdkPath, string.Concat(result.StandardOutput).Trim());

        var latestVersion = Directory.EnumerateDirectories(sdkPath)
            .Where(d => !Path.GetFileName(d).StartsWith("NuGet", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => Path.GetFileName(d))
            .FirstOrDefault() ?? string.Empty;

        if (!string.IsNullOrEmpty(latestVersion))
            throw new DirectoryNotFoundException("Could not find latest dotnet sdk version");

        return Path.Combine(sdkPath, latestVersion);
    }

    public static string GetConsoleTestHostPath() {
        var dotnetSdkPath = GetLatestSdkDirectory();
        if (string.IsNullOrEmpty(dotnetSdkPath))
            throw new DirectoryNotFoundException("Could not find dotnet sdk path");

        var vstestConsolePath = Path.Combine(dotnetSdkPath, "vstest.console.dll");
        if (!File.Exists(vstestConsolePath))
            throw new FileNotFoundException($"Could not find vstest.console.dll in '{dotnetSdkPath}'");

        return vstestConsolePath;
    }
    public static string GetTemplatePackagesDirectory() {
        var templatesPath = Path.Combine(GetRootDirectory(), "templates");
        if (!Directory.Exists(templatesPath))
            throw new DirectoryNotFoundException("Could not find dotnet templates path");

        var directories = Directory.GetDirectories(templatesPath);
        if (directories.Length == 0)
            throw new DirectoryNotFoundException("Could not find dotnet templates directories");

        return directories
            .OrderByDescending(d => Path.GetFileName(d))
            .FirstOrDefault() ?? string.Empty;
    }
}