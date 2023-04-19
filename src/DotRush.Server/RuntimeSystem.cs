using System.Runtime.InteropServices;


namespace DotRush.Server;

public static class RuntimeSystem {
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static string ExecExtension => IsWindows ? ".exe" : "";
}