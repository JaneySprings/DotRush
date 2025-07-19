using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.JsonRpc;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using EmmyLuaLanguageServer = EmmyLua.LanguageServer.Framework.Server.LanguageServer;

namespace DotRush.Roslyn.Server.Handlers.Framework;

public class SolutionDiagnosticsHandler : IJsonHandler {
    private readonly WorkspaceService workspaceService;
    private readonly CodeAnalysisService codeAnalysisService;

    public SolutionDiagnosticsHandler(WorkspaceService workspaceService, CodeAnalysisService codeAnalysisService) {
        this.workspaceService = workspaceService;
        this.codeAnalysisService = codeAnalysisService;
    }

    protected Task Handle(object? request, CancellationToken token) {
        if (workspaceService.Solution != null)
            codeAnalysisService.RequestDiagnosticsPublishing(workspaceService.Solution);
        return Task.CompletedTask;
    }

    public void RegisterHandler(LSPCommunicationBase lspCommunication) {
        lspCommunication.AddNotificationHandler("dotrush/solutionDiagnostics", delegate (NotificationMessage message, CancellationToken token) {
            return Handle(null, token);
        });
    }
    public void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
    }
    public void RegisterDynamicCapability(EmmyLuaLanguageServer server, ClientCapabilities clientCapabilities) {
    }
}
