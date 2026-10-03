using System.Text.Json.Serialization;
using DotRush.Protocol;
using DotRush.Protocol.Handlers;
using DotRush.Protocol.Models;
using DotRush.Roslyn.Server.Services;

namespace DotRush.Roslyn.Server.Handlers.ExternalAccess;

public class ReloadWorkspaceHandler : IHandler {
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

    public void RegisterHandler(LanguageServer server) {
        server.AddNotificationHandler<ReloadWorkspaceParams>("dotrush/reloadWorkspace", Handle);
    }
}

public class ReloadWorkspaceParams {
    [JsonPropertyName("workspaceFolders")]
    public List<WorkspaceFolder>? WorkspaceFolders { get; set; }
}