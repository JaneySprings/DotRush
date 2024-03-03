using DotRush.Server.Extensions;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Reflection;
using System.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;

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
                    Debug.WriteLine($"Creating instance of analyzer '{x.AsType()}' failed, error: {ex}");
                    return null;
                }
            })
            .Where(x => x != null)!;
    }

    public async Task PublishDiagnosticsAsync() {
        ResetCancellationToken();
        var cancellationToken = compilationTokenSource.Token;
        var documentPaths = Diagnostics.GetOpenedDocuments();
        await ServerExtensions.SafeHandlerAsync(async () => {
            await Task.Delay(500, cancellationToken); // Input delay
            if (!configurationService.EnableRoslynAnalyzers())
                await DiagnoseAsync(documentPaths, cancellationToken);
            else
                await AnalyzerDiagnoseAsync(documentPaths, cancellationToken);
        });
    }
    private async Task DiagnoseAsync(IEnumerable<string> documentPaths, CancellationToken cancellationToken) {
        Diagnostics.ClearDocumentDiagnostics(documentPaths);
        var projectIds = this.solutionService.Solution?.GetProjectIdsWithDocumentsFilePaths(documentPaths);
        if (projectIds == null)
            return;

        foreach (var projectId in projectIds) {
            var project = this.solutionService.Solution?.GetProject(projectId);
            if (project == null)
                return;

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation == null)
                return;

            var diagnostics = compilation.GetDiagnostics(cancellationToken);
            foreach (var documentPath in documentPaths) {
                var currentFileDiagnostic = diagnostics.Where(d => d.Location.SourceTree?.FilePath == documentPath);
                Diagnostics.AppendDocumentDiagnostics(documentPath, currentFileDiagnostic, project);
            }
        }
    }
    private async Task AnalyzerDiagnoseAsync(IEnumerable<string> documentPaths, CancellationToken cancellationToken) {
        Diagnostics.ClearDocumentDiagnostics(documentPaths);
        var projectIds = this.solutionService.Solution?.GetProjectIdsWithDocumentsFilePaths(documentPaths);
        if (projectIds == null)
            return;

        foreach (var projectId in projectIds) {
            var project = this.solutionService.Solution?.GetProject(projectId);
            if (project == null)
                return;

            var diagnosticAnalyzers = project.AnalyzerReferences
                .SelectMany(x => x.GetAnalyzers(project.Language))
                .Concat(this.embeddedAnalyzers)
                .ToImmutableArray();

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation == null)
                return;

            var compilationWithAnalyzers = compilation.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions);
            var diagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync(cancellationToken);
            foreach (var documentPath in documentPaths) {
                var currentFileDiagnostic = diagnostics.Where(d => d.Location.SourceTree?.FilePath == documentPath);
                Diagnostics.AppendDocumentDiagnostics(documentPath, currentFileDiagnostic, project);
            }
        }
    }

    private void OnDiagnosticsCollectionChanged(object? sender, DiagnosticsCollectionChangedEventArgs e) {
        serverFacade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams() {
            Uri = DocumentUri.From(e.DocumentPath),
            Diagnostics = new Container<Diagnostic>(e.ServerDiagnostics),
            // Version = version,
        });
    }
    private void ResetCancellationToken() {
        compilationTokenSource.Cancel();
        compilationTokenSource?.Dispose();
        compilationTokenSource = new CancellationTokenSource();
    }
}