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
    private IEnumerable<DiagnosticAnalyzer> embeddedAnalyzers;

    public CancellationTokenSource CompilationTokenSource { get; private set; }
    public Dictionary<string, FileDiagnostics> Diagnostics { get; private set; }

    public CompilationService(WorkspaceService solutionService, ConfigurationService configurationService) {
        this.configurationService = configurationService;
        this.solutionService = solutionService;

        embeddedAnalyzers = Enumerable.Empty<DiagnosticAnalyzer>();
        CompilationTokenSource = new CancellationTokenSource();
        Diagnostics = new Dictionary<string, FileDiagnostics>();
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

    public void EnsureDocumentOpened(string documentPath) {
        if (!Diagnostics.ContainsKey(documentPath) && documentPath.IsSupportedDocument())
            Diagnostics.Add(documentPath, new FileDiagnostics());
    }
    public void ResetCancellationToken() {
        CompilationTokenSource.Cancel();
        CompilationTokenSource?.Dispose();
        CompilationTokenSource = new CancellationTokenSource();
    }
    public async Task PushTotalDiagnosticsAsync(string targetDocumentPath, int? version, ILanguageServerFacade serverFacade, CancellationToken cancellationToken) {
        var documentPaths = Diagnostics.Keys.ToList();
        
        var tasks = documentPaths.Select(it => PushDiagnosticsAsync(it, version, serverFacade, cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (!configurationService.IsRoslynAnalyzersEnabled())
            return;

        await PushAnalyzerDiagnosticsAsync(targetDocumentPath, documentPaths, version, serverFacade, cancellationToken);
    }


    private async Task PushDiagnosticsAsync(string documentPath, int? version, ILanguageServerFacade serverFacade, CancellationToken cancellationToken) {
        await ServerExtensions.SafeHandlerAsync(async () => {
            if (!Diagnostics.ContainsKey(documentPath))
                return;

            await DiagnoseAsync(documentPath, cancellationToken);
            var syntaxDiagnostics = Diagnostics[documentPath].GetSyntaxServerDiagnostics();
            serverFacade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams() {
                Uri = DocumentUri.From(documentPath),
                Diagnostics = new Container<Diagnostic>(syntaxDiagnostics),
                Version = version,
            });
        });
    }
    private async Task PushAnalyzerDiagnosticsAsync(string targetDocumentPath, IEnumerable<string> documentPaths, int? version, ILanguageServerFacade serverFacade, CancellationToken cancellationToken) {
        await ServerExtensions.SafeHandlerAsync(async () => {
            await AnalyzerDiagnoseAsync(targetDocumentPath, documentPaths, cancellationToken);
            foreach (var documentPath in documentPaths) {
                var analyzerDiagnostics = Enumerable.Empty<Diagnostic>();
                if (Diagnostics.ContainsKey(documentPath))
                    analyzerDiagnostics = Diagnostics[documentPath].GetTotalServerDiagnostics();

                serverFacade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams() {
                    Uri = DocumentUri.From(documentPath),
                    Diagnostics = new Container<Diagnostic>(analyzerDiagnostics),
                    Version = version,
                });
            }
        });
    }

    private async Task DiagnoseAsync(string documentPath, CancellationToken cancellationToken) {
        Diagnostics[documentPath].ClearSyntaxDiagnostics();
        
        var documentIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(documentPath);
        if (documentIds == null)
            return;

        foreach (var documentId in documentIds) {
            var document = this.solutionService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var diagnostics = semanticModel?
                .GetDiagnostics(cancellationToken: cancellationToken)
                .Where(d => File.Exists(d.Location.SourceTree?.FilePath));

            if (diagnostics == null)
                continue;

            Diagnostics[documentPath].AddSyntaxDiagnostics(diagnostics, document.Project);
        }
    }
    private async Task AnalyzerDiagnoseAsync(string targetDocumentPath, IEnumerable<string> documentPaths, CancellationToken cancellationToken) {
        var projectId = this.solutionService.Solution?.GetProjectIdsWithDocumentFilePath(targetDocumentPath).FirstOrDefault();
        var project = this.solutionService.Solution?.GetProject(projectId);
        if (project == null)
            return;

        var diagnosticAnalyzers = project.AnalyzerReferences
            .SelectMany(x => x.GetAnalyzers(project.Language))
            .Concat(this.embeddedAnalyzers)
            .ToImmutableArray();
        
        if (diagnosticAnalyzers.IsEmpty)
            return;

        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation == null)
            return;

        var compilationWithAnalyzers = compilation.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions, cancellationToken);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
        foreach (var documentPath in documentPaths) {
            var fileDiagnostics = diagnostics.Where(d => d.Location.SourceTree?.FilePath == documentPath);
            Diagnostics[documentPath].SetAnalyzerDiagnostics(fileDiagnostics, project);
        }
    }
}