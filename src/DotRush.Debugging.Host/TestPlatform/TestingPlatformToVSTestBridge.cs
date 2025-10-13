using System.Diagnostics;
using System.Net.Sockets;
using DotRush.Common;
using DotRush.Common.Extensions;
using DotRush.Debugging.Host.TestPlatform.Protocol;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using StreamJsonRpc;

namespace DotRush.Debugging.Host.TestPlatform;

public class TestingPlatformToVSTestBridge : IDisposable {
    private readonly RpcTestHostNotificationHandler notificationHandler;
    private readonly JsonRpc mtpRpc;
    private readonly int mtpProcessId;

    private TaskCompletionSource? runCompletionSource;
    private TaskCompletionSource<TestNode[]>? discoverCompletionSource;

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
    public async Task<TestNode[]> DiscoverTestsAsync() {
        if (discoverCompletionSource != null)
            throw new InvalidOperationException("A test discovery is already in progress.");

        discoverCompletionSource = new TaskCompletionSource<TestNode[]>();
        await mtpRpc.InvokeWithParameterObjectAsync("testing/discoverTests", new DiscoverRequest(Guid.NewGuid()));
        var result = await discoverCompletionSource.Task;
        discoverCompletionSource = null;

        return result.WhereNotNull().ToArray();
    }
    public async Task RunTestsAsync(string[] typeFilters) {
        if (runCompletionSource != null)
            throw new InvalidOperationException("A test run is already in progress.");

        var request = new RunRequest(Guid.NewGuid());
        if (typeFilters.Length > 0) {
            var testNodes = await DiscoverTestsAsync();
            request.TestCases = testNodes.Where(it => typeFilters.Contains(it.GetFullyQualifiedName())).ToArray();
        }

        if (notificationHandler.IsDebug)
            notificationHandler.AttachDebuggerToProcess(mtpProcessId);

        runCompletionSource = new TaskCompletionSource();
        await mtpRpc.InvokeWithParameterObjectAsync("testing/runTests", request);
        await runCompletionSource.Task;
        runCompletionSource = null;

        CompleteTestRun();
    }
    public async Task ExitAsync() {
        await mtpRpc.InvokeWithParameterObjectAsync("exit", new object());
        CompleteTestRun(isCanceled: true);
    }

    [JsonRpcMethod("testing/testUpdates/tests")]
    public void HandleTestsUpdates(Guid runId, TestNodeUpdate[]? changes) {
        if (changes == null) { // It returns null when operation is completed
            if (runCompletionSource != null)
                runCompletionSource.SetResult();
            return;
        }

        if (discoverCompletionSource != null) {
            discoverCompletionSource.SetResult(changes.Select(it => it.Node).WhereNotNull().ToArray());
            return;
        }
        if (runCompletionSource != null) {
            var relevantChanges = changes.Where(it => it.Node != null && !it.Node.InProgress);
            var vsTestItems = relevantChanges.Select(it => it.Node).WhereNotNull().Select(it => TestNode.ToTestResult(it)).ToArray();
            notificationHandler.HandleTestRunStatsChange(new TestRunChangedEventArgs(null, vsTestItems, null));
            return;
        }
    }
    [JsonRpcMethod("client/log")]
    public Task LogAsync(object level, string message) {
        notificationHandler.HandleRawMessage(message.Replace("\n", "\r\n"));
        return Task.CompletedTask;
    }
    // [JsonRpcMethod("client/attachDebugger")]
    // public Task AttachDebuggerAsync(AttachDebuggerInfo attachDebuggerInfo) {
    //     notificationHandler.AttachDebuggerToProcess(AttachDebuggerInfo);
    //     return Task.CompletedTask;
    // }

    private void CompleteTestRun(bool isCanceled = false) {
        notificationHandler.HandleTestRunComplete(new TestRunCompleteEventArgs(null, isCanceled, isCanceled, null, null, default), null, null, null);
    }
    public void Dispose() {
        if (runCompletionSource != null) {
            runCompletionSource.TrySetCanceled();
            runCompletionSource = null;
        }
        if (discoverCompletionSource != null) {
            discoverCompletionSource.TrySetCanceled();
            discoverCompletionSource = null;
        }
        mtpRpc.Dispose();
    }
}
