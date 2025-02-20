using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace DotRush.Common.Logging;

public static class LogConfig {
    private static readonly string _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    public static readonly string ErrorLogFile = Path.Combine(_logDir, "Error.log");
    public static readonly string DebugLogFile = Path.Combine(_logDir, "Debug.log");

    public static void InitializeLog() {
        var configuration = new LoggingConfiguration();

        var commonTarget = new FileTarget() {
            FileName = DebugLogFile,
            Layout = "${time}|${message}",
            DeleteOldFileOnStartup = true,
            MaxArchiveFiles = 1,
            ArchiveAboveSize = 1 * 1024 * 1024, //MB
        };
        var commonAsyncTarget = new AsyncTargetWrapper(commonTarget, 500, AsyncTargetWrapperOverflowAction.Discard);
        configuration.AddTarget("log", commonAsyncTarget);

        var errorTarget = new FileTarget() {
            FileName = ErrorLogFile,
            DeleteOldFileOnStartup = true,
            Layout = "${longdate}|${message}",
            MaxArchiveFiles = 1,
            ArchiveAboveSize = 1 * 1024 * 1024, //MB
        };
        var errorAsyncTarget = new AsyncTargetWrapper(errorTarget, 500, AsyncTargetWrapperOverflowAction.Discard);
        configuration.AddTarget("errorLog", errorAsyncTarget);

        configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, commonAsyncTarget));
        configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Error, errorAsyncTarget));

        LogManager.ThrowExceptions = false;
        LogManager.Configuration = configuration;
        LogManager.ReconfigExistingLoggers();
    }
}
