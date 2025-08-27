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

    public void Schedule(Func<Message, Task> action, Message message) {
        if (message is MethodMessage methodMessage && syncronizedMethods.Contains(methodMessage.Method)) {
            action(message).Wait();
        }
        else {
            _ = Task.Run(() => action(message));
        }
    }
}
