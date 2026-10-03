namespace DotRush.Protocol;

public static class ErrorCodes {
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int ServerNotInitialized = -32002;
    public const int RequestFailed = -32803;
    public const int ServerCancelled = -32802;
    public const int ContentModified = -32801;
    public const int RequestCancelled = -32800;
}
