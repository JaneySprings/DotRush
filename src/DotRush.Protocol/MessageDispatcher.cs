namespace DotRush.Protocol;

// Messages are dispatched in the order they were received. An exclusive message waits for all running
// messages and blocks the next ones until it completes, other messages are handled concurrently.
public class MessageDispatcher {
    public virtual bool IsExclusive(string method) {
        return true;
    }
    public virtual Task InvokeAsync(string method, Func<Task> handler) {
        return handler.Invoke();
    }
}
