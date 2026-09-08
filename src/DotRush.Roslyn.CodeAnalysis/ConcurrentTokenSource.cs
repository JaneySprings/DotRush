namespace DotRush.Roslyn.CodeAnalysis;

public class ConcurrentTokenSource {
    private readonly object lockObject = new object();
    private CancellationTokenSource? cancellationSource;

    public CancellationToken Restart() {
        lock (lockObject) {
            cancellationSource?.Dispose();
            cancellationSource = new CancellationTokenSource();
            return cancellationSource.Token;
        }
    }
    public void Cancel() {
        lock (lockObject) {
            cancellationSource?.Cancel();
        }
    }
    public void Complete() {
        lock (lockObject) {
            cancellationSource?.Dispose();
            cancellationSource = null;
        }
    }
}
