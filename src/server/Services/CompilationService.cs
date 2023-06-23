using DotRush.Server.Extensions;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Reflection;
using System.Collections.Immutable;

namespace DotRush.Server.Services;

public class CompilationService {
    public Dictionary<string, FileDiagnostics> Diagnostics { get; }
    public HashSet<DiagnosticAnalyzer> DiagnosticAnalyzers { get; }
    private readonly HashSet<string> documents;
    private readonly SolutionService solutionService;

    public CompilationService(SolutionService solutionService) {
        this.solutionService = solutionService;
        this.documents = new HashSet<string>();
        Diagnostics = new Dictionary<string, FileDiagnostics>();
        DiagnosticAnalyzers = new HashSet<DiagnosticAnalyzer>();

        if (!Directory.Exists(LanguageServer.AnalyzersLocation))
            return;

        foreach (var analyzerPath in Directory.GetFiles(LanguageServer.AnalyzersLocation, "*.dll"))
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

    public void DiagnoseAsync(string currentDocumentPath, ITextDocumentLanguageServer proxy, CancellationToken cancellationToken) {
        ServerExtensions.SafeCancellation(async () => {
            foreach (var documentPath in this.documents) {
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

                var totalDiagnostics = (documentPath == currentDocumentPath)
                    ? Diagnostics[documentPath].SyntaxDiagnostics.ToServerDiagnostics()
                    : Diagnostics[documentPath].GetTotalServerDiagnostics();

                proxy.PublishDiagnostics(new PublishDiagnosticsParams() {
                    Uri = DocumentUri.From(documentPath),
                    Diagnostics = new Container<Diagnostic>(totalDiagnostics),
                });
            }
        });
    }

    public void AnalyzerDiagnoseAsync(string documentPath, ITextDocumentLanguageServer proxy, CancellationToken cancellationToken) {
        ServerExtensions.SafeCancellation(async () => {
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);

            var projectId = this.solutionService.Solution?.GetProjectIdsWithDocumentFilePath(documentPath).FirstOrDefault();
            var project = this.solutionService.Solution?.GetProject(projectId);
            if (project == null || !DiagnosticAnalyzers.Any())
                return;

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation == null)
                return;

            var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(DiagnosticAnalyzers.ToArray()), cancellationToken: cancellationToken);
            var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
            var fileDiagnostics = diagnostics.Where(d => d.Location.SourceTree?.FilePath != null && d.Location.SourceTree?.FilePath == documentPath);

            Diagnostics[documentPath].SetAnalyzerDiagnostics(fileDiagnostics, project);
            proxy.PublishDiagnostics(new PublishDiagnosticsParams() {
                Uri = DocumentUri.From(documentPath),
                Diagnostics = new Container<Diagnostic>(Diagnostics[documentPath].GetTotalServerDiagnostics()),
            });
        });
    }

    public void AddDocument(string documentPath) {
        if (this.documents.Contains(documentPath))
            return;

        this.documents.Add(documentPath);
        Diagnostics.Add(documentPath, new FileDiagnostics());
    }

    public void RemoveDocument(string documentPath, ITextDocumentLanguageServer proxy) {
        if (!this.documents.Contains(documentPath))
            return;

        this.documents.Remove(documentPath);
        Diagnostics.Remove(documentPath);
        proxy.PublishDiagnostics(new PublishDiagnosticsParams() {
            Uri = DocumentUri.From(documentPath),
            Diagnostics = new Container<Diagnostic>(Enumerable.Empty<Diagnostic>()),
        });
    }
}