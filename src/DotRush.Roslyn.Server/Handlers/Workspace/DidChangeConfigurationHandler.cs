using DotRush.Protocol.Handlers;
using DotRush.Protocol.Models;
using DotRush.Roslyn.Server.Services;

namespace DotRush.Roslyn.Server.Handlers.Workspace;

public class DidChangeConfigurationHandler : DidChangeConfigurationHandlerBase {
    private readonly ConfigurationService configurationService;

    public DidChangeConfigurationHandler(ConfigurationService configurationService) {
        this.configurationService = configurationService;
    }

    protected override Task Handle(DidChangeConfigurationParams request, CancellationToken token) {
        configurationService.ChangeConfiguration(request.Settings);
        return Task.CompletedTask;
    }
}