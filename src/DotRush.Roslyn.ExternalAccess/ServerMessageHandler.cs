using DotRush.Roslyn.ExternalAccess.Handlers;
using DotRush.Roslyn.ExternalAccess.Models;
using DotRush.Roslyn.Workspaces;

namespace DotRush.Roslyn.ExternalAccess;

public class ServerMessageHandler : IExternalTypeResolver {
    private DotRushWorkspace workspace;
    
    public ServerMessageHandler(DotRushWorkspace workspace) {
        this.workspace = workspace;
    }

    public string? HandleResolveType(string identifierName, SourceLocation location) {
        return ExternalTypeResolver.Handle(identifierName, location, workspace.Solution);
    }
}
