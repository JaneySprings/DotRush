namespace DotRush.Roslyn.Common.Extensions;

public static class SafeExtensions {
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
            return default(T);
        }
    }
    public static async Task InvokeAsync(Func<Task> action) {
        try {
            await action.Invoke();
        } catch (Exception e) {
            LogException(e);
        }
    }
    public static T Invoke<T>(T fallback, Func<T> action) {
        try {
            return action.Invoke();
        } catch (Exception e) {
            LogException(e);
            return fallback;
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
        SessionLogger.LogError(e);
    }
}
