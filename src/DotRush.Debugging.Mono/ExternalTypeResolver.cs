using System.IO.Pipes;
using Mono.Debugging.Client;
using StreamJsonRpc;

namespace DotRush.Debugging.Mono;

public class ExternalTypeResolver : IDisposable {
    private readonly NamedPipeClientStream? transportStream;
    private JsonRpc? rpcServer;

    public ExternalTypeResolver(string? transportId) {
        if (!string.IsNullOrEmpty(transportId))
            transportStream = new NamedPipeClientStream(".", transportId, PipeDirection.InOut, PipeOptions.Asynchronous);
    }

    public bool TryConnect(int timeoutMs = 5000) {
        if (transportStream == null)
            return false;

        try {
            transportStream.Connect(timeoutMs);
            rpcServer = JsonRpc.Attach(transportStream);
            DebuggerLoggingService.CustomLogger?.LogMessage("Debugger connected to external type resolver");
        } catch (Exception e) {
            DebuggerLoggingService.CustomLogger?.LogMessage($"Failed to connect to external type resolver: {e}");
            return false;
        }

        return true;
    }
    public string? Resolve(string identifierName, SourceLocation location) {
        return rpcServer?.InvokeAsync<string>("HandleResolveType", identifierName, location)?.Result;
    }

    public void Dispose() {
        rpcServer?.Dispose();
        transportStream?.Dispose();
    }
}