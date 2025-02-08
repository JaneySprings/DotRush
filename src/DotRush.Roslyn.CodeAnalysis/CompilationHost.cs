using System.Collections.Immutable;
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
        currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Diagnostics started for {projectIds.Count()} projects");

        workspaceDiagnostics.Clear();

        var diagnostics = new List<DiagnosticContext>();
        var overwriteDiagnostic = true;
        foreach (var projectId in projectIds) {
            var project = solution.GetProject(projectId);
            if (project == null)
                continue;

            currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Diagnostics for {project.Name} started");

            var projectDiagnostics = await GetDiagnosticsAsync(project, enableAnalyzers, cancellationToken);
            if (projectDiagnostics == null)
                continue;

            currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Diagnostics for {project.Name} finished");

            diagnostics.AddRange(projectDiagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project)));
            workspaceDiagnostics.AddDiagnostics(diagnostics, overwriteDiagnostic);
            overwriteDiagnostic = false;
        }

        currentClassLogger.Debug($"{nameof(CompilationHost)}[{cancellationToken.GetHashCode()}]: Diagnostics finished");
        return new ReadOnlyCollection<DiagnosticContext>(diagnostics);
    }

    private async Task<IEnumerable<Diagnostic>?> GetDiagnosticsAsync(Project project, bool enableAnalyzers, CancellationToken cancellationToken) {
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null)
            return null;

        if (!enableAnalyzers)
            return compilation.GetDiagnostics(cancellationToken);

        var diagnosticAnalyzers = diagnosticAnalyzersLoader.GetComponents(project);
        if (diagnosticAnalyzers == null || diagnosticAnalyzers.Length == 0)
            return compilation.GetDiagnostics(cancellationToken);

        var compilationWithAnalyzers = compilation.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions);
        if (compilationWithAnalyzers == null)
            return compilation.GetDiagnostics(cancellationToken);;

        return await compilationWithAnalyzers.GetAllDiagnosticsAsync(cancellationToken);
    }
}
