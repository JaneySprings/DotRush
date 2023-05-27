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
    public HashSet<DiagnosticAnalyzer> DiagnosticAnalyzers { get; private set; }
    private SolutionService solutionService;
    private bool isAnalyzeRequested = false;
    private bool isAnalyzing = false;

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


    public async void DiagnoseProject(string projectPath, ITextDocumentLanguageServer proxy) {
        var projectIds = this.solutionService.Solution?.GetProjectIdsWithFilePath(projectPath);
        if (projectIds == null)
            return;

        var result = new Dictionary<string, List<CodeAnalysis.Diagnostic>>();
        foreach (var projectId in projectIds) {
            var project = this.solutionService.Solution?.GetProject(projectId);
            if (project == null)
                continue;
            
            foreach (var document in project.Documents) {
                var semanticModel = await document.GetSemanticModelAsync();
                var diagnostics = semanticModel?
                    .GetDiagnostics()
                    .Where(d => File.Exists(d.Location.SourceTree?.FilePath));

                if (result.ContainsKey(document.FilePath!))
                    result[document.FilePath!].AddRange(diagnostics!);
                else
                    result.Add(document.FilePath!, diagnostics!.ToList());
            }
        }

        foreach (var diagnostic in result) {
            proxy.PublishDiagnostics(new PublishDiagnosticsParams() {
                Uri = DocumentUri.From(diagnostic.Key),
                Diagnostics = new Container<Diagnostic>(diagnostic.Value.ToServerDiagnostics()),
            });
        }
    }

    public async Task DiagnoseDocument(string documentPath, ITextDocumentLanguageServer proxy, CancellationToken cancellationToken) {
        var documentId = this.solutionService.Solution?.GetDocumentIdsWithFilePath(documentPath).FirstOrDefault();
        var document = this.solutionService.Solution?.GetDocument(documentId);
        if (document == null)
            return;
    
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        var diagnostics = semanticModel?
            .GetDiagnostics(cancellationToken: cancellationToken)
            .Where(d => File.Exists(d.Location.SourceTree?.FilePath));

        if (diagnostics == null)
            return;
        
        if (!Diagnostics.ContainsKey(documentPath))
            Diagnostics.Add(documentPath, new FileDiagnostics());
            
        Diagnostics[documentPath].SetSyntaxDiagnostics(diagnostics);
        proxy.PublishDiagnostics(new PublishDiagnosticsParams() {
            Uri = DocumentUri.From(documentPath),
            Diagnostics = new Container<Diagnostic>(Diagnostics[documentPath]
                .GetTotalDiagnostics()
                .ToServerDiagnostics()
            ),
        });
    }

    public async void AnalyzerDiagnose(string documentPath, ITextDocumentLanguageServer proxy) {
        if (this.isAnalyzing) {
            isAnalyzeRequested = true;
            return;
        }
        
        this.isAnalyzing = true;
        var projectId = this.solutionService.Solution?.GetProjectIdsWithDocumentFilePath(documentPath).FirstOrDefault();
        var project = this.solutionService.Solution?.GetProject(projectId);
        if (project == null) {
            this.isAnalyzeRequested = false;
            this.isAnalyzing = false;
            return;
        }
        
        var compilation = await project.GetCompilationAsync();
        if (compilation == null) {
            this.isAnalyzeRequested = false;
            this.isAnalyzing = false;
            return;
        }

        if (!DiagnosticAnalyzers.Any()) {
            this.isAnalyzeRequested = false;
            this.isAnalyzing = false;
            return;
        }
        
        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(DiagnosticAnalyzers.ToArray()));
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        if (diagnostics == null) {
            if (this.isAnalyzeRequested) {
                this.isAnalyzing = false;
                this.isAnalyzeRequested = false;
                AnalyzerDiagnose(documentPath, proxy);
            }
            this.isAnalyzing = false;
            return;
        }

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

        if (this.isAnalyzeRequested) {
            this.isAnalyzing = false;
            this.isAnalyzeRequested = false;
            AnalyzerDiagnose(documentPath, proxy);
        }
        this.isAnalyzing = false;
    }
}