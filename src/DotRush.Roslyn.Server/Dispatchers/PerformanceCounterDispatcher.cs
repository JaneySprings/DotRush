using System.Diagnostics;
using DotRush.Common.Logging;
using DotRush.Protocol;

namespace DotRush.Roslyn.Server.Dispatchers;

public class PerformanceCounterDispatcher : MessageDispatcher {
    public override async Task InvokeAsync(string method, Func<Task> handler) {
        var stopwatch = Stopwatch.StartNew();
        try {
            await handler.Invoke().ConfigureAwait(false);
        } finally {
            CurrentSessionLogger.Debug($"[PERF]: {method} executed in {stopwatch.ElapsedMilliseconds} ms");
        }
    }
}
