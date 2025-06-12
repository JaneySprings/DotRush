using System.Text.RegularExpressions;
using DotRush.Common.Interop;

namespace DotRush.Common.MSBuild;

public static class MSBuildLocator {
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

        var matches = Regex.Matches(result.StandardOutput.Last(), @"\[(.*?)\]");
        var sdkLocation = matches.Count != 0 ? matches[0].Groups[1].Value : null;

        if (string.IsNullOrEmpty(sdkLocation) || !Directory.Exists(sdkLocation))
            throw new DirectoryNotFoundException("Could not find dotnet sdk");

        return Directory.GetParent(sdkLocation)?.FullName ?? string.Empty;
    }

    public static string GetLatestSdkLocation() {
        var dotnetRootPath = GetRootLocation();
        if (string.IsNullOrEmpty(dotnetRootPath))
            throw new DirectoryNotFoundException("Could not find dotnet root path");

        var sdksPath = Path.Combine(dotnetRootPath, "sdk");
        if (!Directory.Exists(sdksPath))
            throw new DirectoryNotFoundException("Could not find dotnet sdks path");

        var directories = Directory.GetDirectories(sdksPath);
        if (directories.Length == 0)
            throw new DirectoryNotFoundException("Could not find dotnet sdk directories");

        return directories
            .OrderByDescending(d => Path.GetFileName(d))
            .FirstOrDefault() ?? string.Empty;
    }
}