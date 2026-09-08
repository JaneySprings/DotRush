using System.Collections.ObjectModel;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Components;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis;

public class CompilationHost : FixAllContext.DiagnosticProvider, IClearable {
    private readonly DiagnosticAnalyzersLoader diagnosticAnalyzersLoader;
    private readonly DiagnosticCollection workspaceDiagnostics;
    private readonly CurrentClassLogger currentClassLogger;

    public CompilationHost(IAdditionalComponentsProvider additionalComponentsProvider) {
        currentClassLogger = new CurrentClassLogger(nameof(CompilationHost));
        diagnosticAnalyzersLoader = new DiagnosticAnalyzersLoader(additionalComponentsProvider);
        workspaceDiagnostics = new DiagnosticCollection();
    }

    public ReadOnlyDictionary<string, List<DiagnosticContext>> GetDiagnostics() {
        return workspaceDiagnostics.GetDiagnostics();
    }
    public ReadOnlyCollection<DiagnosticContext> GetDiagnosticsByDocumentSpan(Document document, TextSpan span) {
        return workspaceDiagnostics.GetDiagnosticsByDocumentSpan(document, span);
    }
    public void ClearCache() {
        diagnosticAnalyzersLoader.ClearCache();
    }

    public Task AnalyzeAsync(IEnumerable<Document> documents, AnalysisScope compilerScope, AnalysisScope analyzerScope, CancellationToken cancellationToken) {
        return workspaceDiagnostics.Execute(async () => {
            await UpdateCompilerDiagnosticsAsync(documents, compilerScope, cancellationToken);
            await UpdateAnalyzerDiagnosticsAsync(documents, analyzerScope, cancellationToken);
        }, cancellationToken);
    }
    public Task AnalyzeAsync(Solution solution, CancellationToken cancellationToken) {
        return workspaceDiagnostics.Execute(() => DiagnoseWithSuppressorsAsync(solution, cancellationToken), cancellationToken);
    }

    private Task UpdateCompilerDiagnosticsAsync(IEnumerable<Document> documents, AnalysisScope scope, CancellationToken cancellationToken) {
        switch (scope) {
            case AnalysisScope.Document:
                return Task.WhenAll(documents.Select(document => DiagnoseAsync(document, cancellationToken)));

            case AnalysisScope.Project:
                var projects = documents.Select(document => document.Project).DistinctBy(project => project.Id);
                return Task.WhenAll(projects.Select(project => DiagnoseWithSuppressorsAsync(project, scope, cancellationToken)));

            case AnalysisScope.Solution:
                var solution = documents.FirstOrDefault()?.Project.Solution;
                if (solution == null)
                    return Task.CompletedTask;
                return DiagnoseWithSuppressorsAsync(solution, cancellationToken);

            default:
                return Task.CompletedTask;
        }
    }
    private Task UpdateAnalyzerDiagnosticsAsync(IEnumerable<Document> documents, AnalysisScope scope, CancellationToken cancellationToken) {
        switch (scope) {
            case AnalysisScope.Document:
                return Task.WhenAll(documents.Select(document => AnalyzerDiagnoseAsync(document, cancellationToken)));

            case AnalysisScope.Project:
                var projects = documents.Select(document => document.Project).DistinctBy(project => project.Id);
                return Task.WhenAll(projects.Select(project => AnalyzerDiagnoseAsync(project, scope, cancellationToken)));

            case AnalysisScope.Solution:
                var solution = documents.FirstOrDefault()?.Project.Solution;
                if (solution == null)
                    return Task.CompletedTask;
                return AnalyzerDiagnoseAsync(solution, cancellationToken);

            default:
                return Task.CompletedTask;
        }
    }

    #region Analysis
    private async Task DiagnoseAsync(Document document, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        currentClassLogger.Debug($"Compiler analysis for {document.Name} ({document.Project.Name}) started");

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel == null)
            return;

        var diagnostics = semanticModel.GetDiagnostics(null, cancellationToken);
        workspaceDiagnostics.AddDiagnostics(document.Project.Id, diagnostics.Select(diagnostic => new CompilerDiagnosticContext(diagnostic, document, AnalysisScope.Document)));
    }
    private async Task DiagnoseAsync(Project project, AnalysisScope scope, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested)
            return;

        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation == null)
            return;

        var diagnostics = compilation.GetDiagnostics(cancellationToken);
        workspaceDiagnostics.AddDiagnostics(project.Id, diagnostics.Select(diagnostic => new CompilerDiagnosticContext(diagnostic, project, scope)));
    }
    private async Task DiagnoseWithSuppressorsAsync(Project project, AnalysisScope scope, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        currentClassLogger.Debug($"Compiler analysis for {project.Name} started");

        var diagnosticSuppressors = diagnosticAnalyzersLoader.GetSuppressors(project);
        if (diagnosticSuppressors == null || diagnosticSuppressors.Length == 0) {
            await DiagnoseAsync(project, scope, cancellationToken);
            return;
        }

        var compilation = await project.GetCompilationAsync(cancellationToken);
        var compilationWithSuppressors = compilation?.WithAnalyzers(diagnosticSuppressors, project.AnalyzerOptions, concurrentAnalysis: true);
        if (compilationWithSuppressors == null)
            return;

        var diagnostics = await compilationWithSuppressors.GetAllDiagnosticsAsync(cancellationToken);
        workspaceDiagnostics.AddDiagnostics(project.Id, diagnostics.Select(diagnostic => new CompilerDiagnosticContext(diagnostic, project, scope)));
    }
    private Task DiagnoseWithSuppressorsAsync(Solution solution, CancellationToken cancellationToken) {
        return Task.WhenAll(solution.Projects.Select(project => DiagnoseWithSuppressorsAsync(project, AnalysisScope.Solution, cancellationToken)));
    }

    private async Task AnalyzerDiagnoseAsync(Document document, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        currentClassLogger.Debug($"Analyzer analysis for {document.Name} started");

        var project = document.Project;
        var diagnosticAnalyzers = diagnosticAnalyzersLoader.GetComponents(project, x => !x.IsCompilerAnalyzer());
        if (diagnosticAnalyzers.Length == 0)
            return;

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel == null)
            return;

        var compilationWithAnalyzers = semanticModel.Compilation.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions, concurrentAnalysis: true);
        var syntaxDiagnostics = await compilationWithAnalyzers.GetAnalyzerSyntaxDiagnosticsAsync(semanticModel.SyntaxTree, cancellationToken);
        var semanticDiagnostics = await compilationWithAnalyzers.GetAnalyzerSemanticDiagnosticsAsync(semanticModel, null, cancellationToken);
        workspaceDiagnostics.AddDiagnostics(project.Id, syntaxDiagnostics.Concat(semanticDiagnostics).Select(diagnostic => new AnalyzerDiagnosticContext(diagnostic, document, AnalysisScope.Document)));
    }
    private async Task AnalyzerDiagnoseAsync(Project project, AnalysisScope scope, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        currentClassLogger.Debug($"Analyzer analysis for {project.Name} started");

        var diagnosticAnalyzers = diagnosticAnalyzersLoader.GetComponents(project, x => !x.IsCompilerAnalyzer());
        if (diagnosticAnalyzers.Length == 0)
            return;

        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation == null)
            return;

        var compilationWithAnalyzers = compilation.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions, concurrentAnalysis: true);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
        workspaceDiagnostics.AddDiagnostics(project.Id, diagnostics.Select(diagnostic => new AnalyzerDiagnosticContext(diagnostic, project, scope)));
    }
    private Task AnalyzerDiagnoseAsync(Solution solution, CancellationToken cancellationToken) {
        return Task.WhenAll(solution.Projects.Select(project => AnalyzerDiagnoseAsync(project, AnalysisScope.Solution, cancellationToken)));
    }
    #endregion

    #region FixAllContext
    public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) {
        return Task.FromResult(workspaceDiagnostics.GetDiagnosticsByProject(project).Select(d => d.Diagnostic));
    }
    public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken) {
        return Task.FromResult(workspaceDiagnostics.GetDiagnosticsByDocument(document).Select(d => d.Diagnostic));
    }
    public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) {
        return Task.FromResult(Enumerable.Empty<Diagnostic>());
    }
    #endregion
}
