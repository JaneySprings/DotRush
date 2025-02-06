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

    public DocumentDiagnosticsHandler(WorkspaceService workspaceService, CodeAnalysisService codeAnalysisService) {
        this.codeAnalysisService = codeAnalysisService;
        this.workspaceService = workspaceService;
    }
    
    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.DiagnosticProvider = new DiagnosticOptions {
            Identifier = "dotrush",
            InterFileDependencies = true,
        };
    }

    protected override async Task<DocumentDiagnosticReport> Handle(DocumentDiagnosticParams request, CancellationToken token) {
        var projectIds = workspaceService.Solution?.GetProjectIdsWithDocumentFilePath(request.TextDocument.Uri.FileSystemPath);
        if (projectIds == null)
            return new RelatedUnchangedDocumentDiagnosticReport();

        var diagnostics = new List<Diagnostic>();
        foreach (var projectId in projectIds) {
            var project = workspaceService.Solution?.GetProject(projectId);
            if (project == null)
                continue;

            var projectDiagnostics = await codeAnalysisService.CompilationHost.GetDiagnosticsAsync(project, token);
            if (projectDiagnostics == null)
                continue;

            diagnostics.AddRange(projectDiagnostics);
        }

        var curentFileDiagnostics = diagnostics.Where(diagnostic => diagnostic.Location.SourceTree?.FilePath == request.TextDocument.Uri.FileSystemPath);
        var otherFileDiagnostics = diagnostics.Except(curentFileDiagnostics).GroupBy(diagnostic => diagnostic.Location.SourceTree!.FilePath);
        var dict = new Dictionary<DocumentUri, FullOrUnchangeDocumentDiagnosticReport>();
        foreach (var group in otherFileDiagnostics) {
            var uri = group.Key;
            dict.Add(uri, new FullDocumentDiagnosticReport {
                Diagnostics = group.Select(diagnostic => diagnostic.ToServerDiagnostic()).ToList(),
            });
        }

        return new RelatedFullDocumentDiagnosticReport {
            Diagnostics = curentFileDiagnostics
                .Select(diagnostic => diagnostic.ToServerDiagnostic())
                .ToList(),
            RelatedDocuments = dict, 
        };
    }
}