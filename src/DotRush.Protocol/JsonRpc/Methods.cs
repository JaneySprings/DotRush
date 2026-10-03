namespace DotRush.Protocol.JsonRpc;

internal static class Methods {
    public const string Initialize = "initialize";
    public const string Initialized = "initialized";
    public const string Shutdown = "shutdown";
    public const string Exit = "exit";
    public const string CancelRequest = "$/cancelRequest";

    public static bool IsLifecycle(string method) {
        return method == Initialize || method == Initialized || method == Shutdown;
    }
}
