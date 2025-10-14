using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using DotRush.Common.Interop;
using DotRush.Common.Logging;

namespace DotRush.Debugging.Host.TestPlatform;

// https://github.com/microsoft/testfx/blob/9866a77221b818a70721106b9622eac95f81adec/samples/Playground/ServerMode/TestingPlatformClientFactory.cs#L23
public class TestingPlatformHostAdapter : ITestHostAdapter {
    private readonly CurrentClassLogger currentClassLogger;
    private readonly bool attachDebugger;

    public TestingPlatformHostAdapter(bool attachDebugger = false) {
        this.currentClassLogger = new CurrentClassLogger(nameof(TestingPlatformHostAdapter));
        this.attachDebugger = attachDebugger;
    }

    public Task StartSession(string[] testAssemblies, string[] typeFilters) {
        return StartSession(testAssemblies, typeFilters, null);
    }
    public async Task StartSession(string[] testAssemblies, string[] typeFilters, string? runSettingsFilePath) {
        var notificationListener = CreateNotificationListener();
        var listenerPort = ((IPEndPoint)notificationListener.LocalEndpoint).Port;

        foreach (var testAssembly in testAssemblies) {
            var mtpProcess = CreateTestingPlatformProcess(testAssembly, listenerPort, runSettingsFilePath);
            if (mtpProcess == null || mtpProcess.HasExited) {
                currentClassLogger.Error($"Failed to start MTP process for assembly '{testAssembly}'");
                continue;
            }

            var mtpBridge = TestingPlatformToVSTestBridge.Attach(notificationListener, mtpProcess.Id, attachDebugger);
            await mtpBridge.InitializeAsync(Environment.ProcessId);
            await mtpBridge.RunTestsAsync(typeFilters);
            mtpBridge.Dispose();
            mtpProcess.WaitForExit();
            currentClassLogger.Debug($"MTP process for assembly '{testAssembly}' exited with code {mtpProcess.ExitCode}");
            mtpProcess.Dispose();
        }

        notificationListener.Dispose();
        currentClassLogger.Debug("Testing session completed");
    }

    private Process? CreateTestingPlatformProcess(string testAssembly, int port, string? runSettingsFilePath) {
        var requiresDotNetRuntime = testAssembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        var executable = requiresDotNetRuntime ? "dotnet" : testAssembly;
        currentClassLogger.Debug($"Starting process '{testAssembly}' with port '{port}'");
        return new ProcessRunner(executable, new ProcessArgumentBuilder()
            .Conditional(testAssembly, () => requiresDotNetRuntime)
            .Append("--server", "--client-host", "localhost", $"--client-port {port}")
            .Conditional($"--config-file \"{runSettingsFilePath}\"", () => !string.IsNullOrEmpty(runSettingsFilePath)))
            .Start();
    }
    private TcpListener CreateNotificationListener() {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }
}