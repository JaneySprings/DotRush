using DotRush.Server.Extensions;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Reflection;
using System.Diagnostics;

namespace DotRush.Server.Services;

public class CompilationService {
    private readonly WorkspaceService solutionService;
    private IEnumerable<DiagnosticAnalyzer> embeddedAnalyzers;

    public Dictionary<string, FileDiagnostics> Diagnostics { get; }

    public CompilationService(WorkspaceService solutionService) {
        this.embeddedAnalyzers = Enumerable.Empty<DiagnosticAnalyzer>();
        this.solutionService = solutionService;
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

    public async Task DiagnoseAsync(string documentPath, CancellationToken cancellationToken) {
        if (!Diagnostics.ContainsKey(documentPath))
            Diagnostics.Add(documentPath, new FileDiagnostics());
        
        await ServerExtensions.SafeHandlerAsync(async () => {
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
        });
    }

    public async Task AnalyzerDiagnoseAsync(string documentPath, CancellationToken cancellationToken) {
        await ServerExtensions.SafeHandlerAsync(async () => {
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
        });
    }
}