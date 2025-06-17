using DotRush.Common.Extensions;

namespace DotRush.Common.Interop.Android;

public static class AndroidFastDev {
    public static void TryPushAssemblies(string serial, string assetsPath, string applicationId, IProcessLogger? logger) {
        assetsPath = assetsPath.TrimPathEnd();
        if (string.IsNullOrEmpty(assetsPath) || !Directory.Exists(assetsPath)) {
            logger?.OnErrorDataReceived($"[FastDev]: Path '{assetsPath}' is not valid or does not exist.");
            return;
        }
        if (Directory.GetFiles(assetsPath, "*.dll", SearchOption.AllDirectories).Length == 0) {
            logger?.OnErrorDataReceived($"[FastDev]: Skipping push, no assemblies found in '{assetsPath}'");
            return;
        }

        logger?.OnOutputDataReceived($"[FastDev]: Pushing '{assetsPath}' to device...");
        AndroidDebugBridge.Shell(serial, "mkdir", "-p", $"/data/local/tmp/{applicationId}");
        AndroidDebugBridge.Push(serial, assetsPath, $"/data/local/tmp/{applicationId}", logger);

        logger?.OnOutputDataReceived("[FastDev]: Deleting existing assemblies in app directory");
        AndroidDebugBridge.Shell(serial, "run-as", applicationId, "mkdir", "-p", $"/data/user/0/{applicationId}/files"); // Create directory if not exists
        AndroidDebugBridge.Shell(serial, "run-as", applicationId, "rm", "-rf", $"/data/user/0/{applicationId}/files/.__override__"); // Ensure directory is empty

        logger?.OnOutputDataReceived("[FastDev]: Copying assemblies to app directory");
        var assetsName = Path.GetFileName(assetsPath);
        var result = AndroidDebugBridge.ShellResult(serial, "run-as", applicationId, "cp", "-r", $"/data/local/tmp/{applicationId}/{assetsName}", $"/data/user/0/{applicationId}/files/.__override__");
        if (!result.Success)
            logger?.OnErrorDataReceived($"[FastDev]: Failed to copy assemblies to app directory: {result.GetError()}");

        logger?.OnOutputDataReceived("[FastDev]: Cleaning up temporary directory");
        AndroidDebugBridge.Shell(serial, "rm", "-rf", $"/data/local/tmp/{applicationId}");
    }
}