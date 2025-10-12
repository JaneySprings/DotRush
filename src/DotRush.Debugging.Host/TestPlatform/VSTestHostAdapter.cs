using DotRush.Common.Logging;
using DotRush.Common.MSBuild;
using DotRush.Debugging.Host.Extensions;
using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;

namespace DotRush.Debugging.Host.TestPlatform;

public class VSTestHostAdapter : ITestHostAdapter {
    private readonly VsTestConsoleWrapper vsTestConsoleWrapper;
    private readonly RpcTestHostNotificationHandler notificationHandler;
    private readonly CurrentClassLogger currentClassLogger;

    public VSTestHostAdapter(bool attachDebugger = false) {
        var consoleTestHostPath = MSBuildLocator.GetConsoleTestHostLocation();
        vsTestConsoleWrapper = new VsTestConsoleWrapper(consoleTestHostPath);
        notificationHandler = new RpcTestHostNotificationHandler(attachDebugger, vsTestConsoleWrapper.CancelTestRun);
        currentClassLogger = new CurrentClassLogger(nameof(VSTestHostAdapter));
    }

    public Task StartSession(string[] testAssemblies, string[] typeFilters) {
        return StartSession(testAssemblies, typeFilters, null);
    }
    public Task StartSession(string[] testAssemblies, string[] typeFilters, string? runSettingsFilePath) {
        currentClassLogger.Debug("Starting test session:");
        currentClassLogger.Debug($"\tAssemblies: {string.Join(", ", testAssemblies)}");

        var testOptions = new TestPlatformOptions();
        if (typeFilters.Length > 0)
            testOptions.TestCaseFilter = string.Join('|', typeFilters.Select(filter => $"FullyQualifiedName={filter}"));
        currentClassLogger.Debug($"\tFilter: '{testOptions.TestCaseFilter}'");

        string? runSettings = null;
        if (!string.IsNullOrEmpty(runSettingsFilePath) && File.Exists(runSettingsFilePath))
            runSettings = File.ReadAllText(runSettingsFilePath);
        runSettings = NUnitFilterExtensions.UpdateRunSettingsWithNUnitFilter(runSettings, testAssemblies, typeFilters);
        currentClassLogger.Debug($"\tRunSettings: {runSettings}");

        vsTestConsoleWrapper.StartSession();
        vsTestConsoleWrapper.RunTestsWithCustomTestHost(testAssemblies, runSettings, testOptions, notificationHandler, notificationHandler);
        vsTestConsoleWrapper.EndSession();
        return Task.CompletedTask;
    }
}
