using System.Net;
using System.Net.Sockets;
using DotRush.Server.Extensions;

namespace DotRush.Server.Services;

public class CommandsService {
    private readonly WorkspaceService workspaceService;
    private readonly ResolveTypeRequest typeResolveRequest;

    private TcpListener? commandListener;

    public CommandsService(WorkspaceService workspaceService) {
        this.workspaceService = workspaceService;
        this.typeResolveRequest = new ResolveTypeRequest(workspaceService);
    }


    public void ResolveCommand(string targetPath) {
        if (!targetPath.IsSupportedCommand())
            return;

        var content = File.ReadAllText(targetPath);
        if (!string.IsNullOrEmpty(content))
            return;

        var port = EnsureListenerInitialized();
        File.WriteAllText(targetPath, port.ToString());
    }
    public void ResolveCancellation() {
        if (commandListener == null)
            return;
        commandListener.Stop();
        commandListener.Dispose();
        commandListener = null;
    }
    
    private int EnsureListenerInitialized() {
        if (commandListener != null) 
            return ((IPEndPoint)commandListener.LocalEndpoint).Port;
            
        commandListener = new TcpListener(IPAddress.Loopback, 0);
        commandListener.Start();
        StartListening();
        return ((IPEndPoint)commandListener.LocalEndpoint).Port;
    }
    private async void StartListening() {
        await ServerExtensions.SafeHandlerAsync(async () => {
            while (true) {
                using var client = await commandListener!.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream);
                using var writer = new StreamWriter(stream) { AutoFlush = true };

                var command = await reader.ReadLineAsync();
                var parameters = await reader.ReadLineAsync();
                var result = string.Empty;

                if (string.IsNullOrEmpty(command) || string.IsNullOrEmpty(parameters)) {
                    await writer.WriteLineAsync();
                    continue;
                }

                if (command.Equals("ResolveType", StringComparison.OrdinalIgnoreCase))
                    result = await typeResolveRequest.HandleAsync(parameters.Split('|'));

                await writer.WriteLineAsync(result ?? string.Empty);
            }
        });
    }
}