using Mono.Debugging.Soft;
using Mono.Debugging.Client;

namespace DotRush.Debugging.Unity;

public abstract class BaseLaunchAgent {
    protected List<Action> Disposables { get; init; }
    protected LaunchConfiguration Configuration { get; init; }

    protected BaseLaunchAgent(LaunchConfiguration configuration) {
        Disposables = new List<Action>();
        Configuration = configuration;
    }

    public abstract void Connect(SoftDebuggerSession session);
    public abstract void Attach(DebugSession debugSession);

    public void Dispose() {
        foreach (var disposable in Disposables) {
            try {
                disposable.Invoke();
                DebuggerLoggingService.CustomLogger?.LogMessage($"Disposing {disposable.Method.Name}");
            } catch (Exception ex) {
                DebuggerLoggingService.CustomLogger?.LogMessage($"Error while disposing {disposable.Method.Name}: {ex.Message}");
            }
        }

        Disposables.Clear();
    }
}