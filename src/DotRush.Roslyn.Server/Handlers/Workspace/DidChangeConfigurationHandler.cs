using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Configuration;
using EmmyLua.LanguageServer.Framework.Server.Handler;

namespace DotRush.Roslyn.Server.Handlers.Workspace;

public class DidChangeConfigurationHandler : DidChangeConfigurationHandlerBase {
    private readonly ConfigurationService configurationService;

    public DidChangeConfigurationHandler(ConfigurationService configurationService) {
        this.configurationService = configurationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
    }
    protected override Task Handle(DidChangeConfigurationParams request, CancellationToken token) {
        configurationService.ChangeConfiguration(request.Settings);
        return Task.CompletedTask;
    }
}