using System.Collections.ObjectModel;
using DotRush.Roslyn.CodeAnalysis.Components;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotRush.Roslyn.CodeAnalysis;

public class CompilationHost {
    private readonly DiagnosticAnalyzersLoader diagnosticAnalyzersLoader = new DiagnosticAnalyzersLoader();
    private readonly DiagnosticCollection workspaceDiagnostics = new DiagnosticCollection();
    private readonly CurrentClassLogger currentClassLogger = new CurrentClassLogger(nameof(CompilationHost));


    public ReadOnlyCollection<DiagnosticContext> GetDiagnostics() {
        return workspaceDiagnostics.GetDiagnostics();
    }
    public DiagnosticContext? GetDiagnosticContextById(int diagnosticId) {
        return workspaceDiagnostics.GetById(diagnosticId);
    }
    public string GetCollectionToken() {
        return workspaceDiagnostics.GetCollectionToken();
    }

    public async Task<ReadOnlyCollection<DiagnosticContext>> DiagnoseAsync(IEnumerable<ProjectId> projectIds, Solution solution, bool enableAnalyzers, CancellationToken cancellationToken) {
        workspaceDiagnostics.Clear();

        foreach (var projectId in projectIds) {
            var project = solution.GetProject(projectId);
            if (project == null)
                continue;

            currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Diagnostics for {project.Name} started");

            var compilation = await DiagnoseAsync(project, cancellationToken);
            if (compilation != null && enableAnalyzers)
                await DiagnoseWithAnalyzersAsync(project, compilation, cancellationToken);

            currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Diagnostics for {project.Name} finished");
        }

        return new ReadOnlyCollection<DiagnosticContext>(workspaceDiagnostics.GetDiagnostics());
    }

    private async Task<Compilation?> DiagnoseAsync(Project project, CancellationToken cancellationToken) {
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

        if (compilation == null)
            return null;

        var parseDiagnostics = compilation.GetParseDiagnostics(cancellationToken);
        workspaceDiagnostics.AddDiagnostics(parseDiagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project)));

        var declarationDiagnostics = compilation.GetDeclarationDiagnostics(cancellationToken);
        workspaceDiagnostics.AddDiagnostics(declarationDiagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project)));

        var methodBodyDiagnostics = compilation.GetMethodBodyDiagnostics(cancellationToken);
        workspaceDiagnostics.AddDiagnostics(methodBodyDiagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project)));

        return compilation;
    }
    private async Task DiagnoseWithAnalyzersAsync(Project project, Compilation compilation, CancellationToken cancellationToken) {
        var diagnosticAnalyzers = diagnosticAnalyzersLoader.GetComponents(project);
        if (diagnosticAnalyzers == null || diagnosticAnalyzers.Length == 0)
            return;

        var compilationWithAnalyzers = compilation.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions);
        if (compilationWithAnalyzers == null)
            return;

        var analyzerDiagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
        workspaceDiagnostics.AddDiagnostics(analyzerDiagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project)));
    }
}
