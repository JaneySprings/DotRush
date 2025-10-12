using System.Diagnostics;
using System.Net.Sockets;
using DotRush.Common;
using DotRush.Debugging.Host.TestPlatform.Protocol;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using StreamJsonRpc;

namespace DotRush.Debugging.Host.TestPlatform;

public class TestingPlatformToVSTestBridge : IDisposable {
    private readonly RpcTestHostNotificationHandler notificationHandler;
    private readonly JsonRpc mtpRpc;
    private readonly int mtpProcessId;

    private TaskCompletionSource? currentRunCompletionSource;

    private TestingPlatformToVSTestBridge(Stream stream, int mtpPid, bool attachDebugger) {
        mtpProcessId = mtpPid;
        notificationHandler = new RpcTestHostNotificationHandler(attachDebugger, () => _ = ExitAsync());

        var formatter = new SystemTextJsonFormatter { JsonSerializerOptions = JsonSerializerConfig.Options };
        mtpRpc = new JsonRpc(new HeaderDelimitedMessageHandler(stream, stream, formatter), this);
        mtpRpc.TraceSource.Switch.Level = SourceLevels.Off;
        mtpRpc.StartListening();
    }
    public static TestingPlatformToVSTestBridge Attach(TcpListener listener, int mtpPid, bool attachDebugger) {
        var client = listener.AcceptTcpClient();
        return new TestingPlatformToVSTestBridge(client.GetStream(), mtpPid, attachDebugger);
    }

    public Task InitializeAsync(int processId) {
        var request = new InitializeRequest(processId);
        notificationHandler.HandleRawMessage($"{nameof(TestingPlatformToVSTestBridge)}: {request.ClientInfo?.Version}");
        return mtpRpc.InvokeWithParameterObjectAsync("initialize", request);
    }
    // public Task<TestNodeUpdate[]> DiscoverTestsAsync(string[] typeFilters) {

    // }
    public async Task RunTestsAsync(string[] typeFilters) {
        if (currentRunCompletionSource != null) {
            currentRunCompletionSource.TrySetResult();
            currentRunCompletionSource = null;
        }

        currentRunCompletionSource = new TaskCompletionSource();
        if (notificationHandler.IsDebug)
            notificationHandler.AttachDebuggerToProcess(mtpProcessId);

        var request = new RunRequest();
        await mtpRpc.InvokeWithParameterObjectAsync("testing/runTests", request);
        await currentRunCompletionSource.Task;
        CompleteTestRun();
    }
    public async Task ExitAsync() {
        await mtpRpc.InvokeWithParameterObjectAsync("exit", new object());
        CompleteTestRun(isCanceled: true);
    }

    [JsonRpcMethod("testing/testUpdates/tests")]
    public Task TestsUpdateAsync(Guid runId, TestNodeUpdate[]? changes) {
        if (changes == null)
            return Task.CompletedTask;

        var relevantChanges = changes.Where(it => it.Node != null && !it.Node.InProgress);
        var vsTestItems = relevantChanges.Select(it => TestNode.ToTestResult(it.Node!)).ToArray();
        notificationHandler.HandleTestRunStatsChange(new TestRunChangedEventArgs(null, vsTestItems, null));
        if (currentRunCompletionSource != null) {
            currentRunCompletionSource.TrySetResult();
        }
        return Task.CompletedTask;
    }
    [JsonRpcMethod("client/log")]
    public Task LogAsync(object level, string message) {
        notificationHandler.HandleRawMessage(message.Replace("\n", "\r\n"));
        return Task.CompletedTask;
    }
    // [JsonRpcMethod("client/attachDebugger", UseSingleObjectParameterDeserialization = true)]
    // public Task AttachDebuggerAsync(AttachDebuggerInfo attachDebuggerInfo) {
    //     notificationHandler.AttachDebuggerToProcess(AttachDebuggerInfo);
    //     return Task.CompletedTask;
    // }

    private void CompleteTestRun(bool isCanceled = false) {
        notificationHandler.HandleTestRunComplete(new TestRunCompleteEventArgs(null, isCanceled, isCanceled, null, null, default), null, null, null);
    }
    public void Dispose() {
        if (currentRunCompletionSource != null) {
            currentRunCompletionSource.TrySetResult();
            currentRunCompletionSource = null;
        }
        mtpRpc.Dispose();
    }
}
