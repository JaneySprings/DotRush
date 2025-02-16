using System.Collections.Concurrent;
using DotRush.Roslyn.Common.Logging;
using EmmyLua.LanguageServer.Framework.Protocol.JsonRpc;
using EmmyLua.LanguageServer.Framework.Server.Scheduler;

namespace DotRush.Roslyn.Server;

public class ParallelDispatcher : IScheduler {
    private readonly Thread _workerThread;
    private readonly BlockingCollection<Action> _tasks = new BlockingCollection<Action>();
    private readonly string[] syncTable = new[] {
        "textDocument/didOpen",
        "textDocument/didChange",
        "textDocument/didClose",
        "workspace/didChangeWatchedFiles"
    };

    public ParallelDispatcher() {
        _workerThread = new Thread(() => {
            foreach (var task in _tasks.GetConsumingEnumerable()) {
                try {
                    task();
                } catch(Exception e) {
                    CurrentSessionLogger.Error(e);
                }
            }
        });
        _workerThread.IsBackground = true;
        _workerThread.Start();
    }

    public void Schedule(Func<Message, Task> action, Message message) {
        if (message is MethodMessage methodMessage && syncTable.Contains(methodMessage.Method)) {
            _tasks.Add(() => action(message).Wait());
            return;
        }

        _tasks.Add(() => _ = Task.Run(async() => await action(message).ConfigureAwait(false)));
    }
}
