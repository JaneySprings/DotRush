using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using DotRush.Common.MSBuild;
using DotRush.Common.Logging;

namespace DotRush.Debugging.NetCore.TestPlatform;

public class TestHostAdapter {
    private readonly VsTestConsoleWrapper vsTestConsoleWrapper;
    private readonly RpcTestHostNotificationHandler testHostNotificationHandler;
    private readonly CurrentClassLogger currentClassLogger;

    public TestHostAdapter(bool attachDebugger = false) {
        var consoleTestHostPath = MSBuildLocator.GetConsoleTestHostLocation();
        vsTestConsoleWrapper = new VsTestConsoleWrapper(consoleTestHostPath);
        testHostNotificationHandler = new RpcTestHostNotificationHandler(attachDebugger);
        currentClassLogger = new CurrentClassLogger(nameof(TestHostAdapter));
    }

    public void StartSession(string[] testAssemblies, string[] typeFilters) {
        StartSession(testAssemblies, typeFilters, string.Empty);
    }
    public void StartSession(string[] testAssemblies, string[] typeFilters, string runSettingsFilePath) {
        currentClassLogger.Debug("Starting test session:");
        currentClassLogger.Debug($"\tAssemblies: {string.Join(", ", testAssemblies)}");
        currentClassLogger.Debug($"\tFilter: {string.Join(", ", typeFilters)}");
        currentClassLogger.Debug($"\tRunSettings: {runSettingsFilePath}");

        string? runSettings = null;
        TestPlatformOptions? testOptions = null;

        if (File.Exists(runSettingsFilePath))
            runSettings = File.ReadAllText(runSettingsFilePath);
        if (typeFilters.Length > 0)
            testOptions = new TestPlatformOptions {
                TestCaseFilter = string.Join(";", typeFilters),
            };

        vsTestConsoleWrapper.StartSession();
        vsTestConsoleWrapper.RunTestsWithCustomTestHost(testAssemblies, runSettings, testOptions, testHostNotificationHandler, testHostNotificationHandler);
        vsTestConsoleWrapper.EndSession();
    }
}
