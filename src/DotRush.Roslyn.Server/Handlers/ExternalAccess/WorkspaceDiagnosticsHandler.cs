using System.Text.Json;
using DotRush.Common;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.JsonRpc;
using EmmyLua.LanguageServer.Framework.Protocol.Message.TextDocument;
using EmmyLua.LanguageServer.Framework.Server;
using EmmyLua.LanguageServer.Framework.Server.Handler;

namespace DotRush.Roslyn.Server.Handlers.ExternalAccess;

public class WorkspaceDiagnosticsHandler : IJsonHandler {
    private readonly WorkspaceService workspaceService;
    private readonly CodeAnalysisService codeAnalysisService;

    public WorkspaceDiagnosticsHandler(WorkspaceService workspaceService, CodeAnalysisService codeAnalysisService) {
        this.workspaceService = workspaceService;
        this.codeAnalysisService = codeAnalysisService;
    }

    protected Task Handle(CancellationToken token) {
        if (workspaceService.Solution != null)
            codeAnalysisService.RequestDiagnosticsPublishing(workspaceService.Solution);
        return Task.CompletedTask;
    }
    protected Task Handle(DidOpenTextDocumentParams? request, CancellationToken token) {
        var filePath = request?.TextDocument.Uri.FileSystemPath;
        if (!string.IsNullOrEmpty(filePath) && codeAnalysisService.CompilerDiagnosticsScope == AnalysisScope.Document)
            codeAnalysisService.RequestDiagnosticsPublishing(filePath, workspaceService);

        return Task.CompletedTask;
    }

    public void RegisterHandler(LSPCommunicationBase lspCommunication) {
        lspCommunication.AddNotificationHandler("dotrush/solutionDiagnostics", delegate (NotificationMessage message, CancellationToken token) {
            return Handle(token);
        });
        lspCommunication.AddNotificationHandler("dotrush/documentDiagnostics", delegate (NotificationMessage message, CancellationToken token) {
            var request = message.Params?.Deserialize<DidOpenTextDocumentParams>(JsonSerializerConfig.Options);
            return Handle(request, token);
        });
    }
    public void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
    }
    public void RegisterDynamicCapability(LanguageServer server, ClientCapabilities clientCapabilities) {
    }
}
