using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.Options;
using EmmyLua.LanguageServer.Framework.Protocol.Message.DocumentDiagnostic;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class DocumentDiagnosticsHandler : DocumentDiagnosticHandlerBase {
    private readonly WorkspaceService workspaceService;
    private readonly CodeAnalysisService codeAnalysisService;
    private readonly ConfigurationService configurationService;

    public DocumentDiagnosticsHandler(WorkspaceService workspaceService, CodeAnalysisService codeAnalysisService, ConfigurationService configurationService) {
        this.codeAnalysisService = codeAnalysisService;
        this.workspaceService = workspaceService;
        this.configurationService = configurationService;
    }
    
    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.DiagnosticProvider ??= new DiagnosticOptions();
        serverCapabilities.DiagnosticProvider.Identifier = "dotrush";
        serverCapabilities.DiagnosticProvider.InterFileDependencies = true;
    }

    protected override Task<DocumentDiagnosticReport> Handle(DocumentDiagnosticParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync<DocumentDiagnosticReport>(new RelatedUnchangedDocumentDiagnosticReport(), async () => {
            var projectIds = GetProjectIdsWithDocumentFilePath(request.TextDocument.Uri.FileSystemPath);
            if (projectIds == null || workspaceService.Solution == null)
                return new RelatedUnchangedDocumentDiagnosticReport();

            var diagnostics = await codeAnalysisService.CompilationHost.DiagnoseAsync(projectIds, workspaceService.Solution, token).ConfigureAwait(false);
            var curentFileDiagnostics = diagnostics.Where(d => PathExtensions.Equals(d.FilePath, request.TextDocument.Uri.FileSystemPath));
            var otherFileDiagnostics = diagnostics.Except(curentFileDiagnostics).GroupBy(d => d.FilePath);
            
            var relatedDocumentsDiagnostics = new Dictionary<DocumentUri, FullOrUnchangeDocumentDiagnosticReport>();
            foreach (var group in otherFileDiagnostics) {
                if (group.Key == null)
                    continue;

                relatedDocumentsDiagnostics.Add(group.Key, new FullDocumentDiagnosticReport {
                    Diagnostics = group.Select(diagnostic => diagnostic.ToServerDiagnostic()).ToList(),
                });
            }

            return new RelatedFullDocumentDiagnosticReport {
                Diagnostics = curentFileDiagnostics.Select(diagnostic => diagnostic.ToServerDiagnostic()).ToList(),
                RelatedDocuments = relatedDocumentsDiagnostics,
            };
        });
    }

    private IEnumerable<ProjectId>? GetProjectIdsWithDocumentFilePath(string filePath) {
        var projectIds = workspaceService.Solution?.GetProjectIdsWithDocumentFilePath(filePath);
        if (projectIds == null || !projectIds.Any())
            return null;
        
        if (configurationService.UseMultitargetDiagnostics)
            return projectIds;

        return projectIds.Take(1);
    }
}