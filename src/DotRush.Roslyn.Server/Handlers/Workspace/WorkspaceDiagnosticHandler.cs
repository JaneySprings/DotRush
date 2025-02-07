using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.Options;
using EmmyLua.LanguageServer.Framework.Protocol.Message.WorkspaceDiagnostic;
using EmmyLua.LanguageServer.Framework.Server.Handler;

namespace DotRush.Roslyn.Server.Handlers.Workspace;

public class WorkspaceDiagnosticHandler : WorkspaceDiagnosticHandlerBase {
    private readonly CodeAnalysisService codeAnalysisService;

    public WorkspaceDiagnosticHandler(CodeAnalysisService codeAnalysisService) {
        this.codeAnalysisService = codeAnalysisService;
    }
    
    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.DiagnosticProvider ??= new DiagnosticOptions();
        serverCapabilities.DiagnosticProvider.WorkspaceDiagnostics = true;
    }

    protected override Task<WorkspaceDiagnosticReport> Handle(WorkspaceDiagnosticParams request, CancellationToken token) {
        var collectionToken = codeAnalysisService.CompilationHost.GetCollectionToken();
        var previousToken = request.PreviousResultIds.FirstOrDefault()?.Value;
        if (previousToken == collectionToken)
            return Task.FromResult(new WorkspaceDiagnosticReport { Items = new List<WorkspaceDocumentDiagnosticReport>() });
        
        var result = new List<WorkspaceDocumentDiagnosticReport>();
        var diagnostics = codeAnalysisService.CompilationHost.GetDiagnostics();
        
        if (request.PreviousResultIds.Count == 0) {
            foreach (var group in diagnostics.GroupBy(d => d.FilePath)) {
                if (group.Key == null)
                    continue;

                result.Add(new WorkspaceFullDocumentDiagnosticReport {
                    Uri = group.Key,
                    ResultId = collectionToken,
                    Diagnostics = group.Select(c => c.ToServerDiagnostic()).ToList()
                });
            }
        } else {
            foreach (var previousResult in request.PreviousResultIds) {
                result.Add(new WorkspaceFullDocumentDiagnosticReport {
                    Uri = previousResult.Uri,
                    ResultId = collectionToken,
                    Diagnostics = diagnostics
                        .Where(d => PathExtensions.Equals(d.FilePath, previousResult.Uri.FileSystemPath))
                        .Select(c => c.ToServerDiagnostic())
                        .ToList()
                });
            }
        }

        return Task.FromResult(new WorkspaceDiagnosticReport { Items = result });
    }
}