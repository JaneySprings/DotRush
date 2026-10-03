using DotRush.Protocol;

namespace DotRush.Roslyn.Server.Dispatchers;

public class MultiThreadDispatcher : MessageDispatcher {
    private readonly string[] syncronizedMethods = new[] {
        "textDocument/didOpen",
        "textDocument/didChange",
        "textDocument/didClose"
    };

    public override bool IsExclusive(string method) {
        return syncronizedMethods.Contains(method);
    }
}
