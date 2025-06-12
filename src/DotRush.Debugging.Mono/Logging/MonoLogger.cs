using Mono.Debugging.Client;
using NLog;

namespace DotRush.Debugging.Mono.Logging;

public class MonoLogger : ICustomLogger {
    private readonly Logger logger = LogManager.GetCurrentClassLogger();

    public string GetNewDebuggerLogFilename() => nameof(MonoLogger);
    public void LogMessage(string format, params object[] args) => logger.Debug(format, args);
    public void LogAndShowException(string message, Exception ex) => LogError(message, ex);
    public void LogError(string message, Exception ex) => logger.Error($"{message}: {ex}");
}
