using System.Collections.ObjectModel;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Components;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis;

public class CompilationHost {
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

    public async Task AnalyzeAsync(IEnumerable<Document> documents, AnalysisScope compilerScope, AnalysisScope analyzerScope, CancellationToken cancellationToken) {
        BeginAnalysis();
        await UpdateCompilerDiagnosticsAsync(documents, compilerScope, cancellationToken).ConfigureAwait(false);
        await UpdateAnalyzerDiagnosticsAsync(documents, analyzerScope, cancellationToken).ConfigureAwait(false);
        EndAnalysis();
    }
    public async Task AnalyzeAsync(Solution solution, CancellationToken cancellationToken) {
        BeginAnalysis();
        await DiagnoseWithSuppressorsAsync(solution, cancellationToken).ConfigureAwait(false);
        EndAnalysis();
    }

    private void BeginAnalysis() {
        workspaceDiagnostics.BeginUpdate();
    }
    private async Task UpdateCompilerDiagnosticsAsync(IEnumerable<Document> documents, AnalysisScope scope, CancellationToken cancellationToken) {
        if (scope == AnalysisScope.None || !documents.Any())
            return;

        foreach (var document in documents) {
            currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Compiler analysis for {document.Name} started");

            switch (scope) {
                case AnalysisScope.Document:
                    await DiagnoseAsync(document, cancellationToken).ConfigureAwait(false);
                    break;
                case AnalysisScope.Project:
                    await DiagnoseWithSuppressorsAsync(document.Project, cancellationToken).ConfigureAwait(false);
                    break;
                case AnalysisScope.Solution:
                    await DiagnoseWithSuppressorsAsync(document.Project.Solution, cancellationToken).ConfigureAwait(false);
                    return; // Already include all projects and target frameworks
            }

            currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Compiler analysis for {document.Name} finished");
        }
    }
    private async Task UpdateAnalyzerDiagnosticsAsync(IEnumerable<Document> documents, AnalysisScope scope, CancellationToken cancellationToken) {
        if (scope == AnalysisScope.None || !documents.Any())
            return;

        foreach (var document in documents) {
            currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Analyzer analysis for {document.Name} started");

            switch (scope) {
                case AnalysisScope.Document:
                    await AnalyzerDiagnoseAsync(document, cancellationToken).ConfigureAwait(false);
                    break;
                case AnalysisScope.Project:
                    await AnalyzerDiagnoseAsync(document.Project, cancellationToken).ConfigureAwait(false);
                    break;
                case AnalysisScope.Solution:
                    await AnalyzerDiagnoseAsync(document.Project.Solution, cancellationToken).ConfigureAwait(false);
                    return; // Already include all projects and target frameworks
            }

            currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Analyzer analysis for {document.Name} finished");
        }
    }
    private void EndAnalysis() {
        workspaceDiagnostics.EndUpdate();
    }

    #region Analysis
    private async Task DiagnoseAsync(Document document, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested)
            return;

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel == null)
            return;

        var diagnostics = semanticModel.GetDiagnostics(null, cancellationToken);
        workspaceDiagnostics.AddDiagnostics(document.Project.Id, diagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, document)));
    }
    private async Task DiagnoseAsync(Project project, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested)
            return;

        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null)
            return;

        var diagnostics = compilation.GetDiagnostics(cancellationToken);
        workspaceDiagnostics.AddDiagnostics(project.Id, diagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project)));
    }
    private async Task DiagnoseWithSuppressorsAsync(Project project, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested)
            return;

        var diagnosticSuppressors = diagnosticAnalyzersLoader.GetSuppressors(project);
        if (diagnosticSuppressors == null || diagnosticSuppressors.Length == 0) {
            await DiagnoseAsync(project, cancellationToken);
            return;
        }

        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        var compilationWithSuppressors = compilation?.WithAnalyzers(diagnosticSuppressors, project.AnalyzerOptions);
        if (compilationWithSuppressors == null)
            return;

        var diagnostics = await compilationWithSuppressors.GetAllDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        workspaceDiagnostics.AddDiagnostics(project.Id, diagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project)));
    }
    private async Task DiagnoseWithSuppressorsAsync(Solution solution, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested)
            return;

        foreach (var project in solution.Projects)
            await DiagnoseWithSuppressorsAsync(project, cancellationToken).ConfigureAwait(false);
    }
    private async Task AnalyzerDiagnoseAsync(Document document, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested)
            return;

        var project = document.Project;
        var diagnosticAnalyzers = diagnosticAnalyzersLoader.GetComponents(project);
        if (diagnosticAnalyzers == null || diagnosticAnalyzers.Length == 0)
            return;

        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel == null || syntaxTree == null)
            return;

        var compilationWithAnalyzers = semanticModel.Compilation.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions);
        if (compilationWithAnalyzers == null)
            return;

        var syntaxDiagnostics = await compilationWithAnalyzers.GetAnalyzerSyntaxDiagnosticsAsync(syntaxTree, cancellationToken).ConfigureAwait(false);
        workspaceDiagnostics.AddDiagnostics(project.Id, syntaxDiagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, document)));

        var semanticDiagnostics = await compilationWithAnalyzers.GetAnalyzerSemanticDiagnosticsAsync(semanticModel, null, cancellationToken).ConfigureAwait(false);
        workspaceDiagnostics.AddDiagnostics(project.Id, semanticDiagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, document)));
    }
    private async Task AnalyzerDiagnoseAsync(Project project, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested)
            return;

        var diagnosticAnalyzers = diagnosticAnalyzersLoader.GetComponents(project);
        if (diagnosticAnalyzers == null || diagnosticAnalyzers.Length == 0)
            return;

        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        var compilationWithAnalyzers = compilation?.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions);
        if (compilationWithAnalyzers == null)
            return;

        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        workspaceDiagnostics.AddDiagnostics(project.Id, diagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project)));
    }
    private async Task AnalyzerDiagnoseAsync(Solution solution, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested)
            return;

        foreach (var project in solution.Projects)
            await AnalyzerDiagnoseAsync(project, cancellationToken).ConfigureAwait(false);
    }
    #endregion
}
