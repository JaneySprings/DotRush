using System.Collections.Immutable;
using System.Collections.ObjectModel;
using DotRush.Roslyn.CodeAnalysis.Components;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotRush.Roslyn.CodeAnalysis;

public class CompilationHost {
    private readonly DiagnosticAnalyzersLoader diagnosticAnalyzersLoader = new DiagnosticAnalyzersLoader();
    private readonly Dictionary<string, IEnumerable<DiagnosticHolder>> workspaceDiagnostics = new Dictionary<string, IEnumerable<DiagnosticHolder>>();
    private readonly CurrentClassLogger currentClassLogger = new CurrentClassLogger(nameof(CompilationHost));

    public event EventHandler<DiagnosticsCollectionChangedEventArgs>? DiagnosticsChanged;

    public IEnumerable<Diagnostic>? GetDiagnostics(string documentPath) {
        documentPath = documentPath.ToPlatformPath();
        if (!workspaceDiagnostics.TryGetValue(documentPath, out IEnumerable<DiagnosticHolder>? container))
            return null;
        return container.Select(d => d.Diagnostic);
    }
    public DiagnosticHolder? GetDiagnosticById(string documentPath, int diagnosticId) {
        documentPath = documentPath.ToPlatformPath();
        if (!workspaceDiagnostics.TryGetValue(documentPath, out IEnumerable<DiagnosticHolder>? container))
            return null;

        return container.FirstOrDefault(d => d.Id == diagnosticId);
    }

    public async Task DiagnoseAsync(IEnumerable<Project> projects, CancellationToken cancellationToken) {
        currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Diagnostics started for {projects.Count()} projects");

        var allDiagnostics = new HashSet<DiagnosticHolder>();
        foreach (var project in projects) {
            var diagnostics = await GetDiagnosticsAsync(project, cancellationToken);
            if (diagnostics == null)
                continue;

            allDiagnostics.AddRange(diagnostics.Value.Select(diagnostic => new DiagnosticHolder(diagnostic, project)));
        }

        var diagnosticsByDocument = allDiagnostics.Where(d => !string.IsNullOrEmpty(d.FilePath)).GroupBy(d => d.FilePath);
        foreach (var diagnostics in diagnosticsByDocument) {
            workspaceDiagnostics[diagnostics.Key!.ToPlatformPath()] = diagnostics;
            DiagnosticsChanged?.Invoke(this, new DiagnosticsCollectionChangedEventArgs(diagnostics.Key!, diagnostics.Select(d => d.Diagnostic)));
        }

        currentClassLogger.Debug($"{nameof(CompilationHost)}[{cancellationToken.GetHashCode()}]: Diagnostics finished");
    }

    private async Task<ImmutableArray<Diagnostic>?> GetDiagnosticsAsync(Project project, CancellationToken cancellationToken) {
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null)
            return null;

        var diagnosticAnalyzers = diagnosticAnalyzersLoader.GetComponents(project);
        if (diagnosticAnalyzers == null || diagnosticAnalyzers.Length == 0)
            return compilation.GetDiagnostics(cancellationToken);

        var compilationWithAnalyzers = compilation.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions);
        if (compilationWithAnalyzers == null)
            return null;

        return await compilationWithAnalyzers.GetAllDiagnosticsAsync(cancellationToken);
    }
}

public class DiagnosticsCollectionChangedEventArgs : EventArgs {
    public ReadOnlyCollection<Diagnostic> Diagnostics { get; private set; }
    public string FilePath { get; private set; }

    public DiagnosticsCollectionChangedEventArgs(string filePath, IEnumerable<Diagnostic> diagnostics) {
        Diagnostics = new ReadOnlyCollection<Diagnostic>(diagnostics.ToList());
        FilePath = filePath;
    }
}

public sealed class DiagnosticHolder {
    public Diagnostic Diagnostic { get; init; }
    public Project Project { get; init; }
    public int Id { get; init; }

    public string? FilePath => Diagnostic.Location.SourceTree?.FilePath;

    public DiagnosticHolder(Diagnostic diagnostic, Project project) {
        Diagnostic = diagnostic;
        Project = project;
        Id = diagnostic.GetUniqueId();
    }

    public override int GetHashCode() => Id;
    public override bool Equals(object? obj) {
        return this.GetHashCode() == obj?.GetHashCode();
    }
}