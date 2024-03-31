using NLog;

namespace DotRush.Server.Logging;

public static class SessionLogger {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    static SessionLogger() {
        LogConfig.InitializeLog();
    }

    public static void LogError(Exception e) {
        logger.Error(e);
    }
    public static void LogError(string message) {
        logger.Error(message);
    }
    public static void LogDebug(string message) {
        logger.Debug(message);
    }
}