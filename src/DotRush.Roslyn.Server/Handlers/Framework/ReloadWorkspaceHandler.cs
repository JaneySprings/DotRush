using System.Text.Json;
using System.Text.Json.Serialization;
using DotRush.Common;
using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.JsonRpc;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Server;
using EmmyLua.LanguageServer.Framework.Server.Handler;

namespace DotRush.Roslyn.Server.Handlers.Framework;

public class ReloadWorkspaceHandler : IJsonHandler {
    private readonly WorkspaceService workspaceService;
    private readonly NavigationService navigationService;
    private readonly CodeAnalysisService codeAnalysisService;

    public ReloadWorkspaceHandler(WorkspaceService workspaceService, NavigationService navigationService, CodeAnalysisService codeAnalysisService) {
        this.workspaceService = workspaceService;
        this.navigationService = navigationService;
        this.codeAnalysisService = codeAnalysisService;
    }

    protected Task Handle(ReloadWorkspaceParams request, CancellationToken token) {
        if (!workspaceService.InitializeWorkspace())
            return Task.CompletedTask;

        navigationService.ClearCache();
        codeAnalysisService.ClearCache();
        _ = workspaceService.LoadAsync(request.WorkspaceFolders, CancellationToken.None);
        return Task.CompletedTask;
    }

    public void RegisterHandler(LSPCommunicationBase lspCommunication) {
        lspCommunication.AddNotificationHandler("dotrush/reloadWorkspace", delegate (NotificationMessage message, CancellationToken token) {
            ReloadWorkspaceParams? request = message.Params?.Deserialize<ReloadWorkspaceParams>(JsonSerializerConfig.Options);
            return Handle(request ?? new ReloadWorkspaceParams(), token);
        });
    }
    public void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
    }
    public void RegisterDynamicCapability(LanguageServer server, ClientCapabilities clientCapabilities) {
    }
}

public class ReloadWorkspaceParams {
    [JsonPropertyName("workspaceFolders")]
    public List<WorkspaceFolder>? WorkspaceFolders { get; set; }
}