using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Workspaces.Extensions;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.Server.Extensions;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Services;

public class DiagnosticService : DiagnosticHost {
    private const int AnalysisFrequencyMs = 500;

    private readonly WorkspaceService workspaceService;
    private readonly ConfigurationService configurationService;
    private readonly ILanguageServerFacade serverFacade;
    private CancellationTokenSource compilationTokenSource;

    public DiagnosticService(ILanguageServerFacade serverFacade, WorkspaceService workspaceService, ConfigurationService configurationService) {
        this.configurationService = configurationService;
        this.workspaceService = workspaceService;
        this.serverFacade = serverFacade;

        DiagnosticsChanged += OnDiagnosticsCollectionChanged;
        compilationTokenSource = new CancellationTokenSource();
    }

    public override void Initialize() {
        CodeFixProvidersLoader.InitializeEmbeddedComponents();
        if (configurationService.UseRoslynAnalyzers)
            DiagnosticAnalyzersLoader.InitializeEmbeddedComponents();
    }

    public Task PublishDiagnosticsAsync() {
        ResetCancellationToken();
        var cancellationToken = compilationTokenSource.Token;
        var documentPaths = GetOpenedDocuments();
        return SafeExtensions.InvokeAsync(async () => {
            await Task.Delay(AnalysisFrequencyMs, cancellationToken).ConfigureAwait(false); // Input delay
            ClearDocumentDiagnostics(documentPaths);

            var projectIds = workspaceService.Solution?.GetProjectIdsWithDocumentsFilePaths(documentPaths);
            if (projectIds == null)
                return;

            foreach (var projectId in projectIds) {
                var project = workspaceService.Solution?.GetProject(projectId);
                if (project == null)
                    continue;

                var compilation = await DiagnoseAsync(project, documentPaths, cancellationToken).ConfigureAwait(false);
                if (configurationService.UseRoslynAnalyzers && compilation != null)
                    await AnalyzerDiagnoseAsync(project, documentPaths, compilation, cancellationToken).ConfigureAwait(false);
            }
        });
    }

    private void OnDiagnosticsCollectionChanged(object? sender, DiagnosticsCollectionChangedEventArgs e) {
        CurrentSessionLogger.Debug($"Publishing {e.Diagnostics.Count} diagnostics for document: {e.DocumentPath}");
        serverFacade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams() {
            Diagnostics = new Container<ProtocolModels.Diagnostic>(e.Diagnostics.Select(d => d.ToServerDiagnostic())),
            Uri = DocumentUri.FromFileSystemPath(e.DocumentPath),
        });
    }
    private void ResetCancellationToken() {
        compilationTokenSource.Cancel();
        compilationTokenSource?.Dispose();
        compilationTokenSource = new CancellationTokenSource();
    }
}