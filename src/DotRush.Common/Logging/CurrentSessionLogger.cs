using NLog;

namespace DotRush.Common.Logging;

public static class CurrentSessionLogger {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private static readonly bool traceDebugMessages = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTRUSH_TRACE_SERVER"));

    static CurrentSessionLogger() {
        LogConfig.InitializeLog();
    }

    public static void Error(Exception e) {
        logger.Error(e.ToString());
    }
    public static void Error(string message) {
        logger.Error(message);
    }
    public static void Debug(string message) {
#if DEBUG
        logger.Debug(message);
#else
        if (traceDebugMessages)
            logger.Debug(message);
#endif
    }
}
