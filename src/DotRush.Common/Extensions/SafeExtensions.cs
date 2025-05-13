using DotRush.Common.Logging;

namespace DotRush.Common.Extensions;

public static class SafeExtensions {
    internal static bool ThrowOnExceptions { get; set; }

    public static async Task<T> InvokeAsync<T>(T fallback, Func<Task<T>> action) {
        try {
            return await action.Invoke();
        } catch (Exception e) {
            LogException(e);
            return fallback;
        }
    }
    public static async Task<T?> InvokeAsync<T>(Func<Task<T>> action) {
        try {
            return await action.Invoke();
        } catch (Exception e) {
            LogException(e);
            return default;
        }
    }
    public static async Task InvokeAsync(Func<Task> action) {
        try {
            await action.Invoke();
        } catch (Exception e) {
            LogException(e);
        }
    }
    public static T? Invoke<T>(Func<T?> action) where T : class {
        try {
            return action.Invoke();
        } catch (Exception e) {
            LogException(e);
            return null;
        }
    }
    public static T Invoke<T>(T defaultValue, Func<T> action) {
        try {
            return action.Invoke();
        } catch (Exception e) {
            LogException(e);
            return defaultValue;
        }
    }
    public static void Invoke(Action action) {
        try {
            action.Invoke();
        } catch (Exception e) {
            LogException(e);
        }
    }

    private static void LogException(Exception e) {
        if (e is TaskCanceledException || e is OperationCanceledException)
            return;
        CurrentSessionLogger.Error(e);
        if (ThrowOnExceptions)
            throw e;
    }
}
