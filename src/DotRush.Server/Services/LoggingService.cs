using NLog;

namespace DotRush.Server.Services;

public class LoggingService {
    public static LoggingService Instance { get; private set; } = null!;
    private readonly Logger logger = LogManager.GetCurrentClassLogger();


    private LoggingService() {}

    public static void Initialize() {
        var service = new LoggingService();
        Instance = service;
    }


    public void LogMessage(string format, params object[] args) => logger.Debug(format, args);
    public void LogError(string message) => logger.Error(message);
    public void LogError(string message, Exception ex) {
        if (ex == null) {
            logger.Error(message);
            return;
        }
        logger.Error(ex, message);
        var innerException = ex.InnerException;

        while (innerException != null) {
            logger.Error(innerException);
            innerException = innerException.InnerException;
        }
    }
}