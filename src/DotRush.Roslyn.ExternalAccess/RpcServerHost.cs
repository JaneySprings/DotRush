using System.IO.Pipes;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Workspaces;
using StreamJsonRpc;

namespace DotRush.Roslyn.ExternalAccess;

public class RpcServerHost {
    private readonly CurrentClassLogger currentClassLogger;
    private readonly ServerMessageHandler messageHandler;
    private readonly string transportId;

    public RpcServerHost(DotRushWorkspace workspace, string transportId) {
        this.currentClassLogger = new CurrentClassLogger(nameof(RpcServerHost));
        this.messageHandler = new ServerMessageHandler(workspace);
        this.transportId = transportId;
    }

    public async Task RunAsync(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            using var pipeStream = new NamedPipeServerStream(transportId, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            currentClassLogger.Debug("Waiting for connection...");

            await pipeStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

            var jsonRpc = JsonRpc.Attach(pipeStream, messageHandler);
            currentClassLogger.Debug("Connected");

            await jsonRpc.Completion.ConfigureAwait(false);
        }
    }
}