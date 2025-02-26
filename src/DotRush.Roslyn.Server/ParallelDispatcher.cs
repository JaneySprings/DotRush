using System.Collections.Concurrent;
using DotRush.Common.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.JsonRpc;
using EmmyLua.LanguageServer.Framework.Server.Scheduler;

namespace DotRush.Roslyn.Server;

public class ParallelDispatcher : IScheduler {
    private readonly Thread mainThread;
    private readonly Thread workerThread;
    private readonly BlockingCollection<TaskInfo> mainTasks = new BlockingCollection<TaskInfo>();
    private readonly BlockingCollection<Action> workerTasks = new BlockingCollection<Action>();
    private readonly string[] workerHandlersTable = new[] {
        "textDocument/diagnostic",
    };

    public ParallelDispatcher() {
        mainThread = new Thread(() => {
            foreach (var taskInfo in mainTasks.GetConsumingEnumerable()) {
                if (taskInfo.IsWorkerTask) {
                    workerTasks.Add(taskInfo.Task);
                    continue;
                }
                SafeExtensions.Invoke(taskInfo.Task);
            }
        });
        workerThread = new Thread(() => {
            foreach (var task in workerTasks.GetConsumingEnumerable())
                SafeExtensions.Invoke(task);
        });

        mainThread.IsBackground = true;
        workerThread.IsBackground = true;
        mainThread.Start();
        workerThread.Start();
    }

    public void Schedule(Func<Message, Task> action, Message message) {
        var taskInfo = new TaskInfo(() => action(message).Wait());
        if (message is MethodMessage methodMessage && workerHandlersTable.Contains(methodMessage.Method))
            taskInfo.IsWorkerTask = true;
 
        mainTasks.Add(taskInfo);
    }


    private class TaskInfo {
        public Action Task { get; set; }
        public bool IsWorkerTask { get; set; }

        public TaskInfo(Action task) {
            Task = task;
        }
    }
}
