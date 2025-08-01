using System.Text;
using DotRush.Common.Logging;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using StreamJsonRpc;

namespace DotRush.Debugging.NetCore.TestPlatform;

public class RpcTestRunEventsHandler : ITestRunEventsHandler {
    private readonly CurrentClassLogger currentClassLogger;
    private readonly JsonRpc? rpcServer;

    public RpcTestRunEventsHandler() {
        var redirectedTextWriter = new TestRunTextWritter(this);
        Console.SetOut(redirectedTextWriter);
        Console.SetError(redirectedTextWriter);
        Console.SetIn(TextReader.Null);

        currentClassLogger = new CurrentClassLogger(nameof(RpcTestRunEventsHandler));
        rpcServer = JsonRpc.Attach(Console.OpenStandardOutput(), Console.OpenStandardInput());
    }

    public void HandleLogMessage(TestMessageLevel level, string? message) {
        currentClassLogger.Debug($"{level}: {message}");
        _ = rpcServer?.NotifyAsync("HandleMessage", message);
    }
    public void HandleRawMessage(string rawMessage) {
        currentClassLogger.Debug(rawMessage);
        _ = rpcServer?.NotifyAsync("HandleMessage", rawMessage);
    }
    public void HandleTestRunComplete(TestRunCompleteEventArgs testRunCompleteArgs, TestRunChangedEventArgs? lastChunkArgs, ICollection<AttachmentSet>? runContextAttachments, ICollection<string>? executorUris) {
        _ = rpcServer?.NotifyAsync("HandleTestRunComplete", testRunCompleteArgs);
    }
    public void HandleTestRunStatsChange(TestRunChangedEventArgs? testRunChangedArgs) {
        _ = rpcServer?.NotifyAsync("HandleTestRunStatsChange", testRunChangedArgs);
    }
    public int LaunchProcessWithDebuggerAttached(TestProcessStartInfo testProcessStartInfo) {
        HandleRawMessage($"Requested to launch process with debugger attached: {testProcessStartInfo.FileName}");
        return 0;
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