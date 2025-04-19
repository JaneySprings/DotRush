using DotRush.Common.Extensions;
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
    private readonly WorkspaceDiagnosticReport emptyReport;

    public WorkspaceDiagnosticHandler(CodeAnalysisService codeAnalysisService) {
        this.codeAnalysisService = codeAnalysisService;
        this.emptyReport = new WorkspaceDiagnosticReport { Items = new List<WorkspaceDocumentDiagnosticReport>() };
    }
    
    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.DiagnosticProvider ??= new DiagnosticOptions();
        serverCapabilities.DiagnosticProvider.WorkspaceDiagnostics = true;
    }
    protected override Task<WorkspaceDiagnosticReport> Handle(WorkspaceDiagnosticParams request, CancellationToken token) {
        return Task.FromResult(SafeExtensions.Invoke(emptyReport, () => {
            var collectionToken = codeAnalysisService.GetDiagnosticsCollectionToken();
            var previousToken = request.PreviousResultIds.FirstOrDefault()?.Value;
            if (previousToken == collectionToken)
                return emptyReport;
            
            var result = new List<WorkspaceDocumentDiagnosticReport>();
            var diagnostics = codeAnalysisService.GetDiagnostics();

            foreach (var pair in diagnostics) {
                result.Add(new WorkspaceFullDocumentDiagnosticReport {
                    Uri = pair.Key,
                    ResultId = collectionToken,
                    Diagnostics = pair.Value.Where(d => !d.IsHiddenInUI()).Select(d => d.ToServerDiagnostic()).ToList()
                });
            }
            
            return new WorkspaceDiagnosticReport { Items = result };
        }));
    }
}