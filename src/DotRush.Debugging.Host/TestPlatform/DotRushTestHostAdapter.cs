namespace DotRush.Debugging.Host.TestPlatform;

public sealed class DotRushTestHostAdapter : ITestHostAdapter {
    private readonly bool attachDebugger;

    public DotRushTestHostAdapter(bool attachDebugger) {
        this.attachDebugger = attachDebugger;
    }

    public Task StartSession(string[] testAssemblies, string[] typeFilters) {
        return StartSession(testAssemblies, typeFilters, null);
    }
    public async Task StartSession(string[] testAssemblies, string[] typeFilters, string? runSettingsFilePath) {
        var testingPlatformAssemblies = testAssemblies.Where(it => IsTestingPlatformRequired(it)).ToArray();
        var vsTestAssemblies = testAssemblies.Except(testingPlatformAssemblies).ToArray();

        if (vsTestAssemblies.Length > 0) {
            var vsTestHost = new VSTestHostAdapter(attachDebugger);
            await vsTestHost.StartSession(vsTestAssemblies, typeFilters, runSettingsFilePath);
        }
        if (testingPlatformAssemblies.Length > 0) {
            var mtpHost = new TestingPlatformHostAdapter(attachDebugger);
            await mtpHost.StartSession(testingPlatformAssemblies, typeFilters, runSettingsFilePath);
        }
    }

    private bool IsTestingPlatformRequired(string assemblyPath) {
        var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
        if (assemblyDirectory == null)
            return false;

        var vsTestHostPath = Path.Combine(assemblyDirectory, "testhost.dll");
        return !File.Exists(vsTestHostPath);
    }
}