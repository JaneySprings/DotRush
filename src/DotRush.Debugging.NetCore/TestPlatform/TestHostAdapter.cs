using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using DotRush.Common.MSBuild;

namespace DotRush.Debugging.NetCore.TestPlatform;

public class TestHostAdapter {
    private VsTestConsoleWrapper vsTestConsoleWrapper;
    private ITestRunEventsHandler testRunEventsHandler;

    public TestHostAdapter(ITestRunEventsHandler testRunEventsHandler) {
        this.testRunEventsHandler = testRunEventsHandler;
        var consoleTestHostPath = MSBuildLocator.GetConsoleTestHostLocation();
        vsTestConsoleWrapper = new VsTestConsoleWrapper(consoleTestHostPath);
    }

    public void StartSession(string[] testAssemblies, string[] typeFilters) {
        StartSession(testAssemblies, typeFilters, string.Empty);
    }
    public void StartSession(string[] testAssemblies, string[] typeFilters, string runSettingsFilePath) {
        string? runSettings = null;
        TestPlatformOptions? testOptions = null;

        if (File.Exists(runSettingsFilePath))
            runSettings = File.ReadAllText(runSettingsFilePath);
        if (typeFilters.Length > 0)
            testOptions = new TestPlatformOptions { TestCaseFilter = string.Join(";", typeFilters) };

        vsTestConsoleWrapper.StartSession();
        vsTestConsoleWrapper.RunTests(testAssemblies, runSettings, testOptions, testRunEventsHandler);
        vsTestConsoleWrapper.EndSession();
    }
}

// public class RunHandler : ITestRunEventsHandler {
//     void ITestMessageEventHandler.HandleLogMessage(TestMessageLevel level, string? message) {
//         CurrentSessionLogger.Debug($"{level}: {message}");
//     }
//     void ITestMessageEventHandler.HandleRawMessage(string rawMessage) {
//         CurrentSessionLogger.Debug($"Raw: {rawMessage}");
//     }
//     void ITestRunEventsHandler.HandleTestRunComplete(TestRunCompleteEventArgs testRunCompleteArgs, TestRunChangedEventArgs? lastChunkArgs, ICollection<AttachmentSet>? runContextAttachments, ICollection<string>? executorUris) {
//         throw new NotImplementedException();
//     }
//     void ITestRunEventsHandler.HandleTestRunStatsChange(TestRunChangedEventArgs? testRunChangedArgs) {
//         throw new NotImplementedException();
//     }
//     int ITestRunEventsHandler.LaunchProcessWithDebuggerAttached(TestProcessStartInfo testProcessStartInfo) {
//         throw new NotImplementedException();
//     }
// }