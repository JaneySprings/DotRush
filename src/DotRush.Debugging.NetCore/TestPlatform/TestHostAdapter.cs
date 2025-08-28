using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using DotRush.Common.MSBuild;
using DotRush.Common.Logging;
using DotRush.Common.Extensions;

namespace DotRush.Debugging.NetCore.TestPlatform;

public class TestHostAdapter {
    private readonly VsTestConsoleWrapper vsTestConsoleWrapper;
    private readonly RpcTestHostNotificationHandler notificationHandler;
    private readonly CurrentClassLogger currentClassLogger;

    public TestHostAdapter(bool attachDebugger = false) {
        var consoleTestHostPath = MSBuildLocator.GetConsoleTestHostLocation();
        vsTestConsoleWrapper = new VsTestConsoleWrapper(consoleTestHostPath);
        notificationHandler = new RpcTestHostNotificationHandler(attachDebugger, vsTestConsoleWrapper.CancelTestRun);
        currentClassLogger = new CurrentClassLogger(nameof(TestHostAdapter));
    }

    public void StartSession(string[] testAssemblies, string[] typeFilters) {
        StartSession(testAssemblies, typeFilters, null);
    }
    public void StartSession(string[] testAssemblies, string[] typeFilters, string? runSettingsFilePath) {
        currentClassLogger.Debug("Starting test session:");
        currentClassLogger.Debug($"\tAssemblies: {string.Join(", ", testAssemblies)}");

        var testOptions = new TestPlatformOptions();
        if (typeFilters.Length > 0)
            testOptions.TestCaseFilter = string.Join('|', typeFilters.Select(filter => $"FullyQualifiedName~{filter}"));
        currentClassLogger.Debug($"\tFilter: '{testOptions.TestCaseFilter}'");

        var runSettings = GetNUnitFilterHack(testAssemblies, typeFilters);
        if (!string.IsNullOrEmpty(runSettingsFilePath) && File.Exists(runSettingsFilePath))
            runSettings = File.ReadAllText(runSettingsFilePath);
        currentClassLogger.Debug($"\tRunSettings: {runSettings}");

        vsTestConsoleWrapper.StartSession();
        vsTestConsoleWrapper.RunTestsWithCustomTestHost(testAssemblies, runSettings, testOptions, notificationHandler, notificationHandler);
        vsTestConsoleWrapper.EndSession();
    }

    // https://github.com/nunit/nunit-vs-adapter/issues/71
    private static string? GetNUnitFilterHack(string[] testAssemblies, string[] typeFilters) {
        if (testAssemblies.Length == 0 || typeFilters.Length == 0)
            return null;

        bool hasNUnitAssembly = false;
        foreach (var assembly in testAssemblies) {
            var assemblyDirectory = Path.GetDirectoryName(assembly)!;
            var assemblies = Directory.GetFiles(assemblyDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            hasNUnitAssembly = assemblies.Any(a => Path.GetFileName(a).Contains("NUnit", StringComparison.OrdinalIgnoreCase));
            if (hasNUnitAssembly)
                break;
        }

        if (!hasNUnitAssembly)
            return null;

        return @$"<?xml version=""1.0"" encoding=""utf-8""?>
<RunSettings>
    <NUnit>
        <Where>{string.Join(" or ", typeFilters.Select(filter => $"test=~/{filter}/"))}</Where>
    </NUnit>
</RunSettings>";
    }
}
