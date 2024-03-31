using DotRush.Server.Extensions;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Reflection;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using DotRush.Server.Logging;
using Microsoft.CodeAnalysis;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using FileSystemExtensions = DotRush.Server.Extensions.FileSystemExtensions;

namespace DotRush.Server.Services;

public class CompilationService {
    private readonly WorkspaceService solutionService;
    private readonly ConfigurationService configurationService;
    private readonly ILanguageServerFacade serverFacade;
    private IEnumerable<DiagnosticAnalyzer> embeddedAnalyzers;
    private CancellationTokenSource compilationTokenSource;

    public DiagnosticsCollection Diagnostics { get; private set; }

    public CompilationService(ILanguageServerFacade serverFacade, WorkspaceService solutionService, ConfigurationService configurationService) {
        this.configurationService = configurationService;
        this.solutionService = solutionService;
        this.serverFacade = serverFacade;
        

        embeddedAnalyzers = Enumerable.Empty<DiagnosticAnalyzer>();
        compilationTokenSource = new CancellationTokenSource();
        Diagnostics = new DiagnosticsCollection();

        Diagnostics.DiagnosticsChanged += OnDiagnosticsCollectionChanged;
    }
    public void InitializeEmbeddedAnalyzers() {
        this.embeddedAnalyzers = Assembly.Load(LanguageServer.CodeAnalysisFeaturesAssembly).DefinedTypes
            .Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(DiagnosticAnalyzer)))
            .Select(x => {
                try {
                    return Activator.CreateInstance(x.AsType()) as DiagnosticAnalyzer;
                } catch (Exception ex) {
                    SessionLogger.LogError($"Creating instance of analyzer '{x.AsType()}' failed, error: {ex}");
                    return null;
                }
            })
            .Where(x => x != null)!;
        SessionLogger.LogDebug($"Initialized {embeddedAnalyzers.Count()} embeded analyzers");
    }

    public async Task PublishDiagnosticsAsync() {
        ResetCancellationToken();
        var cancellationToken = compilationTokenSource.Token;
        var documentPaths = Diagnostics.GetOpenedDocuments();
        await ServerExtensions.SafeHandlerAsync(async () => {
            await Task.Delay(500, cancellationToken); // Input delay
            Diagnostics.ClearDocumentDiagnostics(documentPaths);

            var projectIds = this.solutionService.Solution?.GetProjectIdsWithDocumentsFilePaths(documentPaths);
            if (projectIds == null)
                return;

            foreach (var projectId in projectIds) {
                var compilation = await DiagnoseAsync(projectId, documentPaths, cancellationToken);
                if (configurationService.UseRoslynAnalyzers && compilation != null)
                    await AnalyzerDiagnoseAsync(projectId, documentPaths, compilation, cancellationToken);
            }
        });
    }
    private async Task<Compilation?> DiagnoseAsync(ProjectId projectId, IEnumerable<string> documentPaths, CancellationToken cancellationToken) {
        var project = this.solutionService.Solution?.GetProject(projectId);
        if (project == null)
            return null;

        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation == null)
            return null;

        var diagnostics = compilation.GetDiagnostics(cancellationToken);
        foreach (var documentPath in documentPaths) {
            var currentFileDiagnostic = diagnostics.Where(d => FileSystemExtensions.PathEquals(d.Location.SourceTree?.FilePath, documentPath));
            Diagnostics.AppendDocumentDiagnostics(documentPath, currentFileDiagnostic, project);
        }

        return compilation;
    }
    private async Task AnalyzerDiagnoseAsync(ProjectId projectId, IEnumerable<string> documentPaths, Compilation compilation, CancellationToken cancellationToken) {
        var project = this.solutionService.Solution?.GetProject(projectId);
        if (project == null)
            return;

        var diagnosticAnalyzers = project.AnalyzerReferences
            .SelectMany(x => x.GetAnalyzers(project.Language))
            .Concat(this.embeddedAnalyzers)
            .ToImmutableArray();

        var compilationWithAnalyzers = compilation.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
        foreach (var documentPath in documentPaths) {
            var currentFileDiagnostic = diagnostics.Where(d => FileSystemExtensions.PathEquals(d.Location.SourceTree?.FilePath, documentPath));
            Diagnostics.AppendDocumentDiagnostics(documentPath, currentFileDiagnostic, project);
        }
    }

    private void OnDiagnosticsCollectionChanged(object? sender, DiagnosticsCollectionChangedEventArgs e) {
        SessionLogger.LogDebug($"Publishing diagnostics for document: {e.DocumentPath}");
        serverFacade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams() {
            Uri = DocumentUri.FromFileSystemPath(e.DocumentPath),
            Diagnostics = new Container<ProtocolModels.Diagnostic>(e.ServerDiagnostics),
            // Version = version,
        });
    }
    private void ResetCancellationToken() {
        compilationTokenSource.Cancel();
        compilationTokenSource?.Dispose();
        compilationTokenSource = new CancellationTokenSource();
    }
}