using DotRush.Server.Extensions;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using CodeAnalysis = Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Reflection;
using System.Collections.Immutable;

namespace DotRush.Server.Services;

public class CompilationService {
    public Dictionary<string, FileDiagnostics> Diagnostics { get; }
    public HashSet<DiagnosticAnalyzer> DiagnosticAnalyzers { get; }
    private readonly SolutionService solutionService;

    public CompilationService(SolutionService solutionService) {
        this.solutionService = solutionService;
        this.Diagnostics = new Dictionary<string, FileDiagnostics>();
        DiagnosticAnalyzers = new HashSet<DiagnosticAnalyzer>();

        if (!Directory.Exists(Program.AnalyzersLocation))
            return;

        foreach (var analyzerPath in Directory.GetFiles(Program.AnalyzersLocation, "*.dll"))
            AddAnalyzersWithAssemblyPath(analyzerPath);
    }

    private void AddAnalyzersWithAssemblyName(string assemblyName) {
        AddAnalyzers(Assembly.Load(assemblyName));
    }
    private void AddAnalyzersWithAssemblyPath(string assemblyPath) {
        AddAnalyzers(Assembly.LoadFrom(assemblyPath));
    }
    private void AddAnalyzers(Assembly assembly) {
        var analyzers = assembly.DefinedTypes
            .Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(DiagnosticAnalyzer)))
            .Select(x => {
                try {
                    return Activator.CreateInstance(x.AsType()) as DiagnosticAnalyzer;
                } catch (Exception ex) {
                    LoggingService.Instance.LogError($"Creating instance of analyzer '{x.AsType()}' failed, error: {ex}");
                    return null;
                }
            }).Where(x => x != null);

        foreach (var analyzer in analyzers)
            DiagnosticAnalyzers.Add(analyzer!);
    }

    public async void DiagnoseAsync(string documentPath, ITextDocumentLanguageServer proxy, CancellationToken cancellationToken) {
        var documentId = this.solutionService.Solution?.GetDocumentIdsWithFilePath(documentPath).FirstOrDefault();
        var document = this.solutionService.Solution?.GetDocument(documentId);
        if (document == null)
            return;

        try {
            await DiagnoseDocumentAsync(document, cancellationToken);

            proxy.PublishDiagnostics(new PublishDiagnosticsParams() {
                Uri = DocumentUri.From(documentPath),
                Diagnostics = new Container<Diagnostic>(Diagnostics[documentPath]
                    .GetTotalDiagnostics()
                    .ToServerDiagnostics()
                ),
            });

            await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken);
            await AnalyzerDiagnoseAsync(documentPath, proxy, cancellationToken);
        } catch {
            return;
        }   
    }

    private async Task DiagnoseDocumentAsync(CodeAnalysis.Document document, CancellationToken cancellationToken) {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        var diagnostics = semanticModel?
            .GetDiagnostics(cancellationToken: cancellationToken)
            .Where(d => File.Exists(d.Location.SourceTree?.FilePath));

        if (diagnostics == null)
            return;

        if (!Diagnostics.ContainsKey(document.FilePath!))
            Diagnostics.Add(document.FilePath!, new FileDiagnostics());

        Diagnostics[document.FilePath!].SetSyntaxDiagnostics(diagnostics);
    }

    private async Task AnalyzerDiagnoseAsync(string documentPath, ITextDocumentLanguageServer proxy, CancellationToken cancellationToken) {
        var projectId = this.solutionService.Solution?.GetProjectIdsWithDocumentFilePath(documentPath).FirstOrDefault();
        var project = this.solutionService.Solution?.GetProject(projectId);
        if (project == null || !DiagnosticAnalyzers.Any())
            return;

        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation == null)
            return;

        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(DiagnosticAnalyzers.ToArray()), cancellationToken: cancellationToken);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
        var fileDiagnostics = diagnostics.Where(d => d.Location.SourceTree?.FilePath == documentPath);

        if (!Diagnostics.ContainsKey(documentPath))
            Diagnostics.Add(documentPath, new FileDiagnostics());

        Diagnostics[documentPath].SetAnalyzerDiagnostics(fileDiagnostics);
        proxy.PublishDiagnostics(new PublishDiagnosticsParams() {
            Uri = DocumentUri.From(documentPath),
            Diagnostics = new Container<Diagnostic>(Diagnostics[documentPath]
                .GetTotalDiagnostics()
                .ToServerDiagnostics()
            ),
        });
    }

    public void ClearAnalyzersDiagnostics(string documentPath, ITextDocumentLanguageServer proxy) {
        if (!Diagnostics.ContainsKey(documentPath))
            return;

        Diagnostics[documentPath].ClearAnalyzersDiagnostics();
        proxy.PublishDiagnostics(new PublishDiagnosticsParams() {
            Uri = DocumentUri.From(documentPath),
            Diagnostics = new Container<Diagnostic>(Diagnostics[documentPath]
                .GetTotalDiagnostics()
                .ToServerDiagnostics()
            ),
        });
    }
}