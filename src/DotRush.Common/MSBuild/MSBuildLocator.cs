using System.Text.RegularExpressions;
using DotRush.Common.Interop;

namespace DotRush.Common.MSBuild;

public static partial class MSBuildLocator {
    [GeneratedRegex(@"\[(.*?)\]")]
    private static partial Regex DotNetSdkPathRegex();

    public static FileInfo DotNetTool {
        get {
            var path = Path.Combine(MSBuildLocator.GetRootLocation(), "dotnet" + RuntimeInfo.ExecExtension);
            if (!File.Exists(path))
                throw new FileNotFoundException("Could not find 'dotnet' tool");

            return new FileInfo(path);
        }
    }


    public static string GetRootLocation() {
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_ROOT");

        if (!string.IsNullOrEmpty(dotnet) && Directory.Exists(dotnet))
            return dotnet;

        if (RuntimeInfo.IsWindows)
            dotnet = Path.Combine("C:", "Program Files", "dotnet");
        else if (RuntimeInfo.IsMacOS)
            dotnet = Path.Combine("/usr", "local", "share", "dotnet");
        else
            dotnet = Path.Combine("/usr", "share", "dotnet");

        if (Directory.Exists(dotnet))
            return dotnet;

        var result = new ProcessRunner("dotnet" + RuntimeInfo.ExecExtension, new ProcessArgumentBuilder()
            .Append("--list-sdks"))
            .WaitForExit();

        if (!result.Success)
            throw new FileNotFoundException("Could not find dotnet tool");

        var matches = DotNetSdkPathRegex().Matches(result.StandardOutput.Last());
        var sdkLocation = matches.Count != 0 ? matches[0].Groups[1].Value : null;

        if (string.IsNullOrEmpty(sdkLocation) || !Directory.Exists(sdkLocation))
            throw new DirectoryNotFoundException("Could not find dotnet sdk");

        return Directory.GetParent(sdkLocation)?.FullName ?? string.Empty;
    }
    public static string GetLatestSdkLocation() {
        var sdkPath = Path.Combine(MSBuildLocator.GetSdksLocation(), MSBuildLocator.GetLatestSdkVersion());
        if (!Directory.Exists(sdkPath))
            throw new DirectoryNotFoundException("Could not find actual dotnet sdk directory");

        return sdkPath;
    }
    public static string GetLatestSdkVersion() {
        var result = new ProcessRunner("dotnet" + RuntimeInfo.ExecExtension, new ProcessArgumentBuilder()
           .Append("--version"))
           .WaitForExit();

        if (result.Success)
            return string.Concat(result.StandardOutput).Trim();

        var sdksLocation = MSBuildLocator.GetSdksLocation();
        return Directory.EnumerateDirectories(sdksLocation)
            .Where(d => !Path.GetFileName(d).StartsWith("NuGet", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => Path.GetFileName(d))
            .FirstOrDefault() ?? string.Empty;
    }

    public static string GetConsoleTestHostLocation() {
        var dotnetSdkPath = GetLatestSdkLocation();
        if (string.IsNullOrEmpty(dotnetSdkPath))
            throw new DirectoryNotFoundException("Could not find dotnet sdk path");

        var vstestConsolePath = Path.Combine(dotnetSdkPath, "vstest.console.dll");
        if (!File.Exists(vstestConsolePath))
            throw new FileNotFoundException("Could not find vstest.console.dll");

        return vstestConsolePath;
    }
    public static string GetTemplatePackagesLocation() {
        var templatesPath = Path.Combine(GetRootLocation(), "templates");
        if (!Directory.Exists(templatesPath))
            throw new DirectoryNotFoundException("Could not find dotnet templates path");

        var directories = Directory.GetDirectories(templatesPath);
        if (directories.Length == 0)
            throw new DirectoryNotFoundException("Could not find dotnet templates directories");

        return directories
            .OrderByDescending(d => Path.GetFileName(d))
            .FirstOrDefault() ?? string.Empty;
    }

    private static string GetSdksLocation() {
        var dotnetRootPath = GetRootLocation();
        if (string.IsNullOrEmpty(dotnetRootPath))
            throw new DirectoryNotFoundException("Could not find dotnet root path");

        var sdksPath = Path.Combine(dotnetRootPath, "sdk");
        if (!Directory.Exists(sdksPath))
            throw new DirectoryNotFoundException("Could not find dotnet sdks path");

        return sdksPath;
    }
}