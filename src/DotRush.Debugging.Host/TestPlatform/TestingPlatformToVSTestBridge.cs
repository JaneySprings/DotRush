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

    private TestingPlatformDataCollector? dataCollector;

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
        dataCollector = new TestingPlatformDataCollector(Guid.NewGuid());
        await mtpRpc.InvokeWithParameterObjectAsync("testing/discoverTests", new DiscoverRequest(dataCollector.EventId));
        await dataCollector.Completion;

        return dataCollector.Results;
    }
    public async Task RunTestsAsync(string[] typeFilters) {
        var request = new RunRequest(Guid.NewGuid());
        if (typeFilters.Length > 0) {
            var testNodes = await DiscoverTestsAsync();
            request.TestCases = testNodes.Where(it => typeFilters.Contains(it.GetFullyQualifiedName())).ToArray();
        }

        if (notificationHandler.IsDebug)
            notificationHandler.AttachDebuggerToProcess(mtpProcessId);

        dataCollector = new TestingPlatformDataCollector(request.RunId, changes => {
            var relevantChanges = changes.Where(it => !it.InProgress);
            var vsTestItems = relevantChanges.Select(it => TestNode.ToTestResult(it)).ToArray();
            notificationHandler.HandleTestRunStatsChange(new TestRunChangedEventArgs(null, vsTestItems, null));
        });

        await mtpRpc.InvokeWithParameterObjectAsync("testing/runTests", request);
        await dataCollector.Completion;

        CompleteTestRun();
    }
    public async Task ExitAsync() {
        await mtpRpc.InvokeWithParameterObjectAsync("exit", new object());
        CompleteTestRun(isCanceled: true);
    }

    [JsonRpcMethod("testing/testUpdates/tests")]
    public void HandleTestsUpdates(Guid runId, TestNodeUpdate[]? changes) {
        if (dataCollector == null)
            return;

        dataCollector.HandleTestNodeUpdates(runId, changes);
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
        if (dataCollector != null)
            dataCollector.Dispose();
        mtpRpc.Dispose();
    }


    private class TestingPlatformDataCollector : IDisposable {
        private readonly List<TestNode> collectedNodes;
        private readonly Action<TestNode[]>? collectHandler;
        private readonly TaskCompletionSource completionSource;

        public Guid EventId { get; }
        public Task Completion => completionSource.Task;
        public TestNode[] Results => collectedNodes.ToArray();

        public TestingPlatformDataCollector(Guid eventId, Action<TestNode[]>? collectHandler = null) {
            this.collectedNodes = new List<TestNode>();
            this.completionSource = new TaskCompletionSource();
            this.collectHandler = collectHandler;
            EventId = eventId;
        }

        public void HandleTestNodeUpdates(Guid eventId, TestNodeUpdate[]? changes) {
            if (EventId != eventId)
                return;

            if (changes == null) { // It returns null when operation is completed
                completionSource.TrySetResult();
                return;
            }

            var relevantChanges = changes.Select(it => it.Node).WhereNotNull().ToArray();
            collectedNodes.AddRange(relevantChanges);
            collectHandler?.Invoke(relevantChanges);
        }

        public void Dispose() {
            completionSource.TrySetResult();
            collectedNodes.Clear();
        }
    }
}
