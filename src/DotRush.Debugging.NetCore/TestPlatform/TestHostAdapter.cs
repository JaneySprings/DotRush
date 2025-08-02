using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using DotRush.Common.MSBuild;
using DotRush.Common.Logging;
using DotRush.Common.Extensions;

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
    public void StartSession(string[] rawTestAssemblies, string[] typeFilters, string runSettingsFilePath) {
        var testAssemblies = GetTestAssemblies(rawTestAssemblies);
        currentClassLogger.Debug("Starting test session:");
        currentClassLogger.Debug($"\tAssemblies: {string.Join(", ", testAssemblies)}");
        currentClassLogger.Debug($"\tRunSettings: {runSettingsFilePath}");

        string? runSettings = GetNUnitFilterHack(testAssemblies, typeFilters);
        TestPlatformOptions? testOptions = null;

        if (File.Exists(runSettingsFilePath) && string.IsNullOrEmpty(runSettings))
            runSettings = File.ReadAllText(runSettingsFilePath);
        if (typeFilters.Length > 0)
            testOptions = new TestPlatformOptions {
                TestCaseFilter = string.Join('|', typeFilters.Select(filter => $"FullyQualifiedName={filter}"))
            };

        currentClassLogger.Debug($"\tFilter: {testOptions?.TestCaseFilter}");

        vsTestConsoleWrapper.StartSession();
        vsTestConsoleWrapper.RunTestsWithCustomTestHost(testAssemblies, runSettings, testOptions, testHostNotificationHandler, testHostNotificationHandler);
        vsTestConsoleWrapper.EndSession();
    }

    private static string[] GetTestAssemblies(string[] rawTestAssemblies) {
        return rawTestAssemblies.Select(path => path.Trim('"', '\'').ToPlatformPath()).ToArray();
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
