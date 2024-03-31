
namespace DotRush.Server.Extensions;

public static class LocatorExtensions {
    public static bool TryRegisterDefaults(Action? onError = null) {
        try {
            Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();
            return true;
        } catch (InvalidOperationException) {
            onError?.Invoke();
            return false;
        }
    }
}