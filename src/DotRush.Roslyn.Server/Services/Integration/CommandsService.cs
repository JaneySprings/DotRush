using System.Net;
using System.Net.Sockets;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Server.Extensions;

namespace DotRush.Roslyn.Server.Services;

public class CommandsService {
    private readonly WorkspaceService workspaceService;
    private readonly ResolveTypeRequest typeResolveRequest;
    private readonly TcpListener commandListener;

    public CommandsService(WorkspaceService workspaceService) {
        this.workspaceService = workspaceService;
        typeResolveRequest = new ResolveTypeRequest(workspaceService);
        commandListener = new TcpListener(IPAddress.Loopback, 0);
    }

    public void ResolveCommand(string targetPath) {
        var content = File.ReadAllText(targetPath);
        if (!string.IsNullOrEmpty(content))
            return;

        if (!commandListener.Server.IsBound) 
            commandListener.Start();

        var port = ((IPEndPoint)commandListener.LocalEndpoint).Port;
        StartListening();
        File.WriteAllText(targetPath, port.ToString());
    }
    public void ResolveCancellation() {
        commandListener?.Stop();
    }
    
    private async void StartListening() {
        await SafeExtensions.InvokeAsync(async () => {
            using var client = await commandListener!.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream) { AutoFlush = true };
    
            while (client.Connected) {
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