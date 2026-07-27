using DotRush.Common.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.JsonRpc;
using EmmyLua.LanguageServer.Framework.Server.Scheduler;

namespace DotRush.Roslyn.Server.Dispatchers;

public class MultiThreadDispatcher : IScheduler {
    private readonly string[] syncronizedMethods = new[] {
        "textDocument/didOpen",
        "textDocument/didChange",
        "textDocument/didClose",
        "textDocument/willSave",
        "textDocument/willSaveWaitUntil"
    };
    private readonly List<Task> runningTasks = new List<Task>();
    private readonly object runningTasksLock = new object();

    public void Schedule(Func<Message, Task> action, Message message) {
        if (message is MethodMessage methodMessage && syncronizedMethods.Contains(methodMessage.Method)) {
            WaitForRunningTasks();
            action(message).Wait();
            return;
        }

        var task = Task.Run(() => action(message));
        lock (runningTasksLock)
            runningTasks.Add(task);

        task.ContinueWith(t => {
            lock (runningTasksLock)
                runningTasks.Remove(t);
        }, TaskContinuationOptions.ExecuteSynchronously);
    }

    private void WaitForRunningTasks() {
        Task[] tasks;
        lock (runningTasksLock)
            tasks = runningTasks.ToArray();

        if (tasks.Length != 0)
            SafeExtensions.Invoke(() => Task.WaitAll(tasks));
    }
}
