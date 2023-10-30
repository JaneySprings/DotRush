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
    private CancellationTokenSource cancellationTokenSource;
    private IEnumerable<DiagnosticAnalyzer> embeddedAnalyzers;

    public Dictionary<string, FileDiagnostics> Diagnostics { get; }

    public CompilationService(WorkspaceService solutionService, ConfigurationService configurationService) {
        this.configurationService = configurationService;
        this.solutionService = solutionService;
        
        embeddedAnalyzers = Enumerable.Empty<DiagnosticAnalyzer>();
        cancellationTokenSource = new CancellationTokenSource();
        
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
        if (!Diagnostics.ContainsKey(documentPath))
            Diagnostics.Add(documentPath, new FileDiagnostics());
    }

    public async void StartPushingDiagnostics(ILanguageServerFacade serverFacade, int? version) {
        cancellationTokenSource.Cancel();
        cancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = cancellationTokenSource.Token;
        foreach (var documentPath in Diagnostics.Keys) {
            if (cancellationToken.IsCancellationRequested)
                break;

            await PushDocumentDiagnosticsAsync(documentPath, version, serverFacade, cancellationToken);
        }
    }

    public async Task PushDocumentDiagnosticsAsync(string documentPath, int? version, ILanguageServerFacade serverFacade, CancellationToken cancellationToken) {
        await ServerExtensions.SafeHandlerAsync(async () => {
            if (Path.GetExtension(documentPath) != ".cs")
                return;

            await DiagnoseAsync(documentPath, cancellationToken);
            var syntaxDiagnostics = Diagnostics[documentPath].GetSyntaxServerDiagnostics();
            serverFacade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams() {
                Uri = DocumentUri.From(documentPath),
                Diagnostics = new Container<Diagnostic>(syntaxDiagnostics),
                Version = version,
            });
        
            if (!configurationService.IsRoslynAnalyzersEnabled())
                return;

            await AnalyzerDiagnoseAsync(documentPath, cancellationToken);
            var totalDiagnostics = Diagnostics[documentPath].GetTotalServerDiagnostics();
            serverFacade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams() {
                Uri = DocumentUri.From(documentPath),
                Diagnostics = new Container<Diagnostic>(totalDiagnostics),
                Version = version,
            });
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

    private async Task AnalyzerDiagnoseAsync(string documentPath, CancellationToken cancellationToken) {
        var projectId = this.solutionService.Solution?.GetProjectIdsWithDocumentFilePath(documentPath).FirstOrDefault();
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
        var fileDiagnostics = diagnostics.Where(d => d.Location.SourceTree?.FilePath != null && d.Location.SourceTree?.FilePath == documentPath);

        Diagnostics[documentPath].SetAnalyzerDiagnostics(fileDiagnostics, project);
    }
}