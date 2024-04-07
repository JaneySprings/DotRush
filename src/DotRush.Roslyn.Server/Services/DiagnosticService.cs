using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
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
using FileSystemExtensions = DotRush.Roslyn.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.Server.Services;

public class DiagnosticService : DiagnosticHost {
    private const int AnalysisFrequencyMs = 500;

    private readonly WorkspaceService solutionService;
    private readonly ConfigurationService configurationService;
    private readonly ILanguageServerFacade serverFacade;
    private CancellationTokenSource compilationTokenSource;

    public DiagnosticService(ILanguageServerFacade serverFacade, WorkspaceService solutionService, ConfigurationService configurationService) {
        this.configurationService = configurationService;
        this.solutionService = solutionService;
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

            var projectIds = solutionService.Solution?.GetProjectIdsWithDocumentsFilePaths(documentPaths);
            if (projectIds == null)
                return;

            foreach (var projectId in projectIds) {
                var compilation = await DiagnoseAsync(projectId, documentPaths, cancellationToken).ConfigureAwait(false);
                if (configurationService.UseRoslynAnalyzers && compilation != null)
                    await AnalyzerDiagnoseAsync(projectId, documentPaths, compilation, cancellationToken).ConfigureAwait(false);
            }
        });
    }
    private async Task<Compilation?> DiagnoseAsync(ProjectId projectId, IEnumerable<string> documentPaths, CancellationToken cancellationToken) {
        var project = solutionService.Solution?.GetProject(projectId);
        if (project == null)
            return null;

        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null)
            return null;

        var diagnostics = compilation.GetDiagnostics(cancellationToken);
        foreach (var documentPath in documentPaths) {
            var currentFileDiagnostic = diagnostics.Where(d => FileSystemExtensions.PathEquals(d.Location.SourceTree?.FilePath, documentPath));
            AppendDocumentDiagnostics(documentPath, currentFileDiagnostic, project);
        }

        return compilation;
    }
    private async Task AnalyzerDiagnoseAsync(ProjectId projectId, IEnumerable<string> documentPaths, Compilation compilation, CancellationToken cancellationToken) {
        var project = solutionService.Solution?.GetProject(projectId);
        if (project == null)
            return;

        var diagnosticAnalyzers = DiagnosticAnalyzersLoader.GetComponents(project);
        var compilationWithAnalyzers = compilation.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var documentPath in documentPaths) {
            var currentFileDiagnostic = diagnostics.Where(d => FileSystemExtensions.PathEquals(d.Location.SourceTree?.FilePath, documentPath));
            AppendDocumentDiagnostics(documentPath, currentFileDiagnostic, project);
        }
    }

    private void OnDiagnosticsCollectionChanged(object? sender, DiagnosticsCollectionChangedEventArgs e) {
        CurrentSessionLogger.Debug($"Publishing {e.Diagnostics.Count} diagnostics for document: {e.DocumentPath}");
        serverFacade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams() {
            Diagnostics = new Container<ProtocolModels.Diagnostic>(e.Diagnostics.Select(d => d.ToServerDiagnostic(e.Source))),
            Uri = DocumentUri.FromFileSystemPath(e.DocumentPath),
        });
    }
    private void ResetCancellationToken() {
        compilationTokenSource.Cancel();
        compilationTokenSource?.Dispose();
        compilationTokenSource = new CancellationTokenSource();
    }
}