using System.Text;
using DotRush.Common.Extensions;
using DotRush.Common.InteropV2;
using DotRush.Common.Logging;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.Interfaces;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using StreamJsonRpc;

namespace DotRush.Debugging.Host.TestPlatform;

public class RpcTestHostNotificationHandler : ITestRunEventsHandler, ITestHostLauncher3 {
    private readonly CurrentClassLogger currentClassLogger;
    private readonly JsonRpc? rpcServer;

    public static bool SuspendCompletion { get; set; } // Continue execution if has pending test runs
    public bool IsDebug { get; init; }

    public RpcTestHostNotificationHandler(bool attachDebugger, Action? endSessionCallback = null) {
        var redirectedTextWriter = new TestRunTextWritter(this);
        Console.SetOut(redirectedTextWriter);
        Console.SetError(redirectedTextWriter);
        Console.SetIn(TextReader.Null);

        IsDebug = attachDebugger;

        currentClassLogger = new CurrentClassLogger(nameof(RpcTestHostNotificationHandler));
        rpcServer = JsonRpc.Attach(Console.OpenStandardOutput(), Console.OpenStandardInput());
        rpcServer.AllowModificationWhileListening = true;
        rpcServer.AddLocalRpcMethod("handleTestRunCancel", () => {
            HandleRawMessage("Cancelling test run...");
            endSessionCallback?.Invoke();
        });
    }

    #region ITestRunEventsHandler
    public void HandleLogMessage(TestMessageLevel level, string? message) {
        currentClassLogger.Debug($"{level}: {message}");
        rpcServer?.NotifyAsync("handleMessage", message).Wait();
    }
    public void HandleRawMessage(string rawMessage) {
        currentClassLogger.Debug(rawMessage);
        rpcServer?.NotifyAsync("handleMessage", rawMessage).Wait();
    }
    public void HandleTestRunComplete(TestRunCompleteEventArgs testRunCompleteArgs, TestRunChangedEventArgs? lastChunkArgs, ICollection<AttachmentSet>? runContextAttachments, ICollection<string>? executorUris) {
        if (SuspendCompletion && testRunCompleteArgs != null && !testRunCompleteArgs.IsCanceled && !testRunCompleteArgs.IsAborted)
            return;

        rpcServer?.NotifyAsync("handleTestRunComplete", testRunCompleteArgs).Wait();
    }
    public void HandleTestRunStatsChange(TestRunChangedEventArgs? testRunChangedArgs) {
        rpcServer?.NotifyAsync("handleTestRunStatsChange", testRunChangedArgs).Wait();
    }
    public int LaunchProcessWithDebuggerAttached(TestProcessStartInfo testProcessStartInfo) {
        HandleRawMessage($"[Error]: Requested to launch process with debugger attached: {testProcessStartInfo.FileName}");
        throw new NotImplementedException();
    }
    #endregion

    #region ITestHostLauncher
    bool ITestHostLauncher3.AttachDebuggerToProcess(AttachDebuggerInfo attachDebuggerInfo, CancellationToken cancellationToken) {
        currentClassLogger.Debug($"Attaching debugger3 to process: {attachDebuggerInfo.ProcessId}");
        return RequestDebuggerAttach(attachDebuggerInfo.ProcessId);
    }
    public bool AttachDebuggerToProcess(int pid) {
        currentClassLogger.Debug($"Attaching debugger2 to process: {pid}");
        return RequestDebuggerAttach(pid);
    }
    bool ITestHostLauncher2.AttachDebuggerToProcess(int pid, CancellationToken cancellationToken) {
        currentClassLogger.Debug($"Attaching debugger2 to process with cancellation: {pid}");
        return RequestDebuggerAttach(pid);
    }
    int ITestHostLauncher.LaunchTestHost(TestProcessStartInfo defaultTestHostStartInfo) {
        return LaunchTestHost(defaultTestHostStartInfo);
    }
    int ITestHostLauncher.LaunchTestHost(TestProcessStartInfo defaultTestHostStartInfo, CancellationToken cancellationToken) {
        return LaunchTestHost(defaultTestHostStartInfo);
    }
    #endregion

    private bool RequestDebuggerAttach(int processId) {
        var result = rpcServer?.InvokeAsync<bool>("attachDebuggerToProcess", processId).Result ?? false;
        if (result) {
            // VSCode resolves debugger after executable is launched,
            // so we need to wait a bit while debugger is attached.
            Thread.Sleep(2000);
        }

        return result;
    }
    private int LaunchTestHost(TestProcessStartInfo startInfo) {
        if (string.IsNullOrEmpty(startInfo.FileName)) {
            currentClassLogger.Error($"{nameof(TestProcessStartInfo.FileName)} is null or empty");
            ArgumentNullException.ThrowIfNull(startInfo.FileName, nameof(startInfo.FileName));
        }

        currentClassLogger.Debug($"Launching test host: {startInfo.FileName} {startInfo.Arguments}");
        return ProcessRunner.CreateProcess(
            executable: startInfo.FileName,
            arguments: startInfo.Arguments ?? string.Empty,
            workingDirectory: startInfo.WorkingDirectory,
            environmentVariables: startInfo.EnvironmentVariables?.ToNotNullDictionary(),
            captureOutput: true,
            displayWindow: false
        ).Id;
    }

    private class TestRunTextWritter : TextWriter {
        private readonly StringBuilder lineBuffer;
        private readonly ITestRunEventsHandler hanler;

        public TestRunTextWritter(ITestRunEventsHandler handler) {
            this.hanler = handler;
            this.lineBuffer = new StringBuilder();
        }

        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char value) {
            if (value == '\n') {
                hanler.HandleRawMessage(lineBuffer.ToString());
                lineBuffer.Clear();
            }
            else if (value != '\r') {
                lineBuffer.Append(value);
            }
        }
    }
}