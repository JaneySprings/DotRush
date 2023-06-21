
namespace DotRush.Server.Extensions;

public static class ServerExtensions {

    public static void SafeCancellation(Func<Task> action) {
        try {
            action.Invoke();
        } catch (OperationCanceledException) {
            // ignore
        }
    }

    public static async Task<T> SafeHandlerAsync<T>(T fallback, Func<Task<T>> action) where T : class {
        try {
            return await action.Invoke();
        } catch (Exception) {
            // ignore
            return fallback;
        }
    }

    public static async Task<T?> SafeHandlerAsync<T>(Func<Task<T>> action) {
        try {
            return await action.Invoke();
        } catch (Exception) {
            // ignore
            return default(T);
        }
    }

    public static async Task SafeHandlerAsync(Func<Task> action) {
        try {
            await action.Invoke();
        } catch (Exception) {
            // ignore
        }
    }

    public static T SafeHandler<T>(T fallback, Func<T> action) where T : class {
        try {
            return action.Invoke();
        } catch (Exception) {
            // ignore
            return fallback;
        }
    }
}