using System.Collections.Concurrent;
using DotRush.Roslyn.Common.Logging;
using EmmyLua.LanguageServer.Framework.Protocol.JsonRpc;
using EmmyLua.LanguageServer.Framework.Server.Scheduler;

namespace DotRush.Roslyn.Server;

public class AsyncDispatcher : IScheduler {
    private readonly Thread _workerThread;
    private readonly BlockingCollection<Action> _tasks = new BlockingCollection<Action>();

    public AsyncDispatcher() {
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
        _tasks.Add(() => _ = action(message));
    }
}
