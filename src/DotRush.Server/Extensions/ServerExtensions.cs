using DotRush.Server.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

namespace DotRush.Server.Extensions;

public static class ServerExtensions {

    public static async Task<T> SafeHandlerAsync<T>(T fallback, Func<Task<T>> action) {
        try {
            return await action.Invoke();
        } catch (Exception e) {
            LogException(e);
            return fallback;
        }
    }
    public static async Task<T?> SafeHandlerAsync<T>(Func<Task<T>> action) {
        try {
            return await action.Invoke();
        } catch (Exception e) {
            LogException(e);
            return default(T);
        }
    }
    public static async Task SafeHandlerAsync(Func<Task> action) {
        try {
            await action.Invoke();
        } catch (Exception e) {
            LogException(e);
        }
    }

    public static T SafeHandler<T>(T fallback, Func<T> action) {
        try {
            return action.Invoke();
        } catch (Exception e) {
            LogException(e);
            return fallback;
        }
    }
    public static void SafeHandler(Action action) {
        try {
            action.Invoke();
        } catch (Exception e) {
            LogException(e);
        }
    }

    public static void ShowError(this ILanguageServerFacade server, string message) {
        server.Window.ShowMessage(new ShowMessageParams {
            Type = MessageType.Error,
            Message = message
        });
        SessionLogger.LogError(message);
    }
    public static void ShowInfo(this ILanguageServerFacade server, string message) {
        server.Window.ShowMessage(new ShowMessageParams {
            Type = MessageType.Info,
            Message = message
        });
        SessionLogger.LogDebug(message);
    }

    private static void LogException(Exception e) {
        if (e is TaskCanceledException || e is OperationCanceledException) 
            return;
        SessionLogger.LogError(e);
    }
}