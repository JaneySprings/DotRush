using DotRush.Roslyn.ExternalAccess;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DotRush.Roslyn.Server.Services;

public class ExternalAccessService {
    private readonly RpcServerHost rpcServerHost;

    public ExternalAccessService(ILanguageServerFacade serverFacade, WorkspaceService workspaceService) {
        rpcServerHost = new RpcServerHost(workspaceService, $"DotRush-{serverFacade.Workspace.ClientSettings.ProcessId}");  
    }

    public Task StartListeningAsync(CancellationToken cancellationToken) {
        return rpcServerHost.RunAsync(cancellationToken);
    }
}