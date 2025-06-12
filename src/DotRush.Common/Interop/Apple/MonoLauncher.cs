using System.Diagnostics;

namespace DotRush.Common.Interop.Apple;

public static class MonoLauncher {
    // https://github.com/xamarin/xamarin-macios/issues/21664
    public static bool UseDeviceCtl { get; set; }

    public static Process TcpTunnel(string serial, int port, IProcessLogger? logger = null) {
        FileInfo tool = AppleSdkLocator.MLaunchTool();
        return new ProcessRunner(tool, new ProcessArgumentBuilder()
            .Append($"--tcp-tunnel={port}:{port}")
            .Append($"--devname={serial}")
            .Conditional("--use-device-ctl=false", () => !MonoLauncher.UseDeviceCtl), logger)
            .Start();
    }
    public static void InstallDev(string serial, string bundlePath, IProcessLogger? logger = null) {
        var tool = AppleSdkLocator.MLaunchTool();
        logger?.OnOutputDataReceived(tool.FullName);
        new ProcessRunner(tool, new ProcessArgumentBuilder()
            .Append("--installdev").AppendQuoted(bundlePath)
            .Append($"--devname={serial}")
            .Append("--install-progress")
            .Conditional("--use-device-ctl=false", () => !MonoLauncher.UseDeviceCtl), logger)
            .WaitForExit();
    }
    private static ProcessRunner LaunchDev(string serial, string bundlePath, IEnumerable<string> arguments, Dictionary<string, string> environment, IProcessLogger? logger = null) {
        var tool = AppleSdkLocator.MLaunchTool();
        var argumentBuilder = new ProcessArgumentBuilder()
            .Append("--launchdev").AppendQuoted(bundlePath)
            .Append($"--devname={serial}")
            .Append("--wait-for-exit");

        foreach (var arg in arguments)
            argumentBuilder.Append($"--argument={arg}");
        foreach (var env in environment)
            argumentBuilder.Append($"--setenv={env.Key}={env.Value}");

        return new ProcessRunner(tool, argumentBuilder, logger);
    }
    private static ProcessRunner LaunchSim(string serial, string bundlePath, IEnumerable<string> arguments, Dictionary<string, string> environment, IProcessLogger? logger = null) {
        var tool = AppleSdkLocator.MLaunchTool();
        logger?.OnOutputDataReceived(tool.FullName);
        var argumentBuilder = new ProcessArgumentBuilder()
            .Append("--launchsim").AppendQuoted(bundlePath)
            .Append($"--device=:v2:udid={serial}");

        foreach (var arg in arguments)
            argumentBuilder.Append($"--argument={arg}");
        foreach (var env in environment)
            argumentBuilder.Append($"--setenv={env.Key}={env.Value}");

        return new ProcessRunner(tool, argumentBuilder, logger);
    }

    public static Process DebugDev(string serial, string bundlePath, int port, Dictionary<string, string>? environment = null, IProcessLogger? logger = null) {
        environment ??= new Dictionary<string, string>();
        environment.TryAdd("__XAMARIN_DEBUG_PORT__", $"{port}");
        return MonoLauncher.LaunchDev(serial, bundlePath,
            arguments: new List<string> {
                "-monodevelop-port", $"{port}"
            },
            environment,
            logger
        ).Start();
    }
    public static Process DebugSim(string serial, string bundlePath, int port, Dictionary<string, string>? environment = null, IProcessLogger? logger = null) {
        environment ??= new Dictionary<string, string>();
        environment.TryAdd("__XAMARIN_DEBUG_PORT__", $"{port}");
        return MonoLauncher.LaunchSim(serial, bundlePath,
            arguments: new List<string> {
                "-monodevelop-port", $"{port}"
            },
            environment,
            logger
        ).Start();
    }
}