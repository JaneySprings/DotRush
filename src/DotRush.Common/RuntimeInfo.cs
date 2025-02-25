using System.Runtime.InteropServices;

namespace DotRush.Common;

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

    public static string GetArchitecture() {
        return IsAarch64 ? "arm64" : "x86_64";
    }
    public static string GetArchitecture64() {
        return IsAarch64 ? "arm64" : "x64";
    }
    public static string GetOperationSystem() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "win32";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "darwin";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";

        return "unknown";
    }
}