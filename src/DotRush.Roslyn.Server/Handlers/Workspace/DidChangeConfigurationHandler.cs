using System.Text.Json;
using System.Text.Json.Serialization;
using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.JsonRpc;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using EmmyLuaLanguageServer = EmmyLua.LanguageServer.Framework.Server.LanguageServer;

namespace DotRush.Roslyn.Server.Handlers.Workspace;

public class DidChangeConfigurationHandler : IJsonHandler {
    private readonly ConfigurationService configurationService;

    public DidChangeConfigurationHandler(ConfigurationService configurationService) {
        this.configurationService = configurationService;
    }

    protected Task Handle(DidChangeConfigurationParams request, CancellationToken token) {
        configurationService.ChangeConfiguration(request.Settings);
        return Task.CompletedTask;
    }


	public void RegisterHandler (LSPCommunicationBase lspCommunication) {
		lspCommunication.AddNotificationHandler ("workspace/didChangeConfiguration", delegate(NotificationMessage message, CancellationToken token) {
			DidChangeConfigurationParams? request = message.Params?.Deserialize<DidChangeConfigurationParams>();
			return Handle (request ?? new DidChangeConfigurationParams(), token);
		});
	}
	public void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
    }
	public void RegisterDynamicCapability(EmmyLuaLanguageServer server, ClientCapabilities clientCapabilities) {
	}
}

public class DidChangeConfigurationParams {
    [JsonPropertyName("settings")]
    public LSPAny? Settings { get; set; }
}