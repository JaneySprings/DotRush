using DotRush.Common.Logging;

namespace DotRush.Debugging.Host.TestPlatform;

public sealed class DotRushTestHostAdapter : ITestHostAdapter {
    private readonly CurrentClassLogger currentClassLogger;
    private readonly bool attachDebugger;

    public DotRushTestHostAdapter(bool attachDebugger) {
        this.currentClassLogger = new CurrentClassLogger(nameof(DotRushTestHostAdapter));
        this.attachDebugger = attachDebugger;
    }

    public Task StartSession(string[] testAssemblies, string[] typeFilters) {
        return StartSession(testAssemblies, typeFilters, null);
    }
    public async Task StartSession(string[] testAssemblies, string[] typeFilters, string? runSettingsFilePath) {
        var testingPlatformAssemblies = testAssemblies.Where(it => IsTestingPlatformRequired(it)).ToArray();
        var vsTestAssemblies = testAssemblies.Except(testingPlatformAssemblies).ToArray();

        RpcTestHostNotificationHandler.SuspendCompletion = testingPlatformAssemblies.Length > 0; // continue for pending sessions
        if (vsTestAssemblies.Length > 0) {
            currentClassLogger.Debug($"Starting VSTest session for: {string.Join(", ", vsTestAssemblies)}");
            var vsTestHost = new VSTestHostAdapter(attachDebugger);
            await vsTestHost.StartSession(vsTestAssemblies, typeFilters, runSettingsFilePath);
        }
        RpcTestHostNotificationHandler.SuspendCompletion = false; // Restore default behavior

        if (testingPlatformAssemblies.Length > 0) {
            currentClassLogger.Debug($"Starting TestingPlatform session for: {string.Join(", ", testingPlatformAssemblies)}");
            var mtpHost = new TestingPlatformHostAdapter(attachDebugger);
            await mtpHost.StartSession(testingPlatformAssemblies, typeFilters, runSettingsFilePath);
        }

        currentClassLogger.Debug("All test sessions passed");
    }

    private bool IsTestingPlatformRequired(string assemblyPath) {
        var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
        if (assemblyDirectory == null)
            return false;

        var vsTestHostAssembly = Path.Combine(assemblyDirectory, "testhost.dll");
        var vsTestHostBinary = Path.Combine(assemblyDirectory, "testhost.exe");
        return !File.Exists(vsTestHostAssembly) && !File.Exists(vsTestHostBinary);
    }
}