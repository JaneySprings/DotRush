using System.Runtime.InteropServices;

namespace DotRush.Roslyn.Common;

public static class RuntimeInfo {
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static bool IsAarch64 => RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

    public static string ExecExtension => IsWindows ? ".exe" : "";
    public static string HomeDirectory => IsWindows
        ? Environment.GetEnvironmentVariable("USERPROFILE")!
        : Environment.GetEnvironmentVariable("HOME")!;
    public static string ProgramX86Directory => IsWindows
        ? Environment.GetEnvironmentVariable("ProgramFiles(x86)")!
        : throw new PlatformNotSupportedException();
}