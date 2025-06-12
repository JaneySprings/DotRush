using System.Diagnostics;

namespace DotRush.Common.Interop.Apple;

// This tool requires the 'Apple Devices' app daemon (AppleMobileDevice) or (usbmuxd) to be running.
// https://www.microsoft.com/store/productId/9NP83LWLPZ9K?ocid=pdpshare
public static class IDeviceTool {
    public static void Installer(string serial, string bundlePath, IProcessLogger? logger = null) {
        var tool = new FileInfo(Path.Combine(AppleSdkLocator.IDeviceLocation(), "ideviceinstaller" + RuntimeInfo.ExecExtension));
        var result = new ProcessRunner(tool, new ProcessArgumentBuilder()
            .Append("--udid").Append(serial)
            .Append("--install").AppendQuoted(bundlePath), logger)
            .WaitForExit();

        if (!result.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.StandardError));
    }
    public static Process Proxy(string serial, int port, IProcessLogger? logger = null) {
        var tool = new FileInfo(Path.Combine(AppleSdkLocator.IDeviceLocation(), "iproxy" + RuntimeInfo.ExecExtension));
        return RuntimeInfo.IsWindows
            ? new ProcessRunner(tool, new ProcessArgumentBuilder()
                .Append($"{port} {port}")
                .Append(serial), logger)
                .Start()
            : new ProcessRunner(tool, new ProcessArgumentBuilder()
                .Append($"{port}:{port}")
                .Append("-u", serial), logger)
                .Start();
    }
}