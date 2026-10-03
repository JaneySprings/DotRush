using DotRush.Protocol;
using DotRush.Protocol.Handlers;
using DotRush.Protocol.Models;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.Server.Services;

namespace DotRush.Roslyn.Server.Handlers.ExternalAccess;

public class WorkspaceDiagnosticsHandler : IHandler {
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

    public void RegisterHandler(LanguageServer server) {
        server.AddNotificationHandler("dotrush/solutionDiagnostics", Handle);
        server.AddNotificationHandler<DidOpenTextDocumentParams>("dotrush/documentDiagnostics", Handle);
    }
}
