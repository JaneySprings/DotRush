namespace DotRush.Common.Logging;

public class CurrentClassLogger {
    private readonly string tag;

    public CurrentClassLogger(string tag) {
        this.tag = tag;
    }

    public void Error(Exception e) {
        CurrentSessionLogger.Error($"[{tag}]: {e}");
    }
    public void Error(string message) {
        CurrentSessionLogger.Error($"[{tag}]: {message}");
    }
    public void Debug(string message) {
        CurrentSessionLogger.Debug($"[{tag}]: {message}");
    }
}