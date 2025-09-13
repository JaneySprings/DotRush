using System.Collections.Concurrent;
using System.Diagnostics;
using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using EmmyLua.LanguageServer.Framework.Protocol.JsonRpc;
using EmmyLua.LanguageServer.Framework.Server.Scheduler;

namespace DotRush.Roslyn.Server.Dispatchers;

public class PerformanceCounterDispatcher : IScheduler {
    private readonly Thread workerThread;
    private readonly BlockingCollection<TaskInfo> taskInfos = new BlockingCollection<TaskInfo>();
    private readonly Stopwatch stopwatch = new Stopwatch();

    public PerformanceCounterDispatcher() {
        workerThread = new Thread(() => {
            foreach (TaskInfo item in taskInfos.GetConsumingEnumerable()) {
                stopwatch.Restart();
                SafeExtensions.Invoke(item.Task);
                stopwatch.Stop();
                CurrentSessionLogger.Debug($"[PERF]: {item.MethodName} executed in {stopwatch.ElapsedMilliseconds} ms");
            }
        }) {
            IsBackground = true
        };
        workerThread.Start();
    }

    public void Schedule(Func<Message, Task> action, Message message) {
        var methodName = message is MethodMessage methodMessage
            ? methodMessage.Method
            : message.GetType().Name;
        taskInfos.Add(new TaskInfo(() => action(message).Wait(), methodName));
    }

    private class TaskInfo {
        public Action Task { get; }
        public string MethodName { get; }

        public TaskInfo(Action task, string methodName) {
            Task = task;
            MethodName = methodName;
        }
    }
}
