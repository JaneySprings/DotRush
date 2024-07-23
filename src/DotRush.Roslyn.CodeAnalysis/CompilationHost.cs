using System.Collections.ObjectModel;
using DotRush.Roslyn.CodeAnalysis.Components;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using FileSystemExtensions = DotRush.Roslyn.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.CodeAnalysis;

public class CompilationHost {
    private readonly DiagnosticAnalyzersLoader diagnosticAnalyzersLoader = new DiagnosticAnalyzersLoader();
    private readonly Dictionary<string, IEnumerable<DiagnosticHolder>> workspaceDiagnostics = new Dictionary<string, IEnumerable<DiagnosticHolder>>();

    public event EventHandler<DiagnosticsCollectionChangedEventArgs>? DiagnosticsChanged;

    public IEnumerable<Diagnostic>? GetDiagnostics(string documentPath) {
        documentPath = FileSystemExtensions.NormalizePath(documentPath);
        if (!workspaceDiagnostics.TryGetValue(documentPath, out IEnumerable<DiagnosticHolder>? container))
            return null;
        return container.Select(d => d.Diagnostic);
    }
    public DiagnosticHolder? GetDiagnosticById(string documentPath, int diagnosticId) {
        documentPath = FileSystemExtensions.NormalizePath(documentPath);
        if (!workspaceDiagnostics.TryGetValue(documentPath, out IEnumerable<DiagnosticHolder>? container))
            return null;

        return container.FirstOrDefault(d => d.Id == diagnosticId);
    }

    public async Task DiagnoseAsync(IEnumerable<Project> projects, CancellationToken cancellationToken) {
        CurrentSessionLogger.Debug($"CompilationHost[{cancellationToken.GetHashCode()}]: Diagnostics started for {projects.Count()} projects");

        var allDiagnostics = new HashSet<DiagnosticHolder>();
        foreach (var project in projects) {
            var compilationWithAnalyzers = await CompileWithAnalyzersAsync(project, cancellationToken).ConfigureAwait(false);
            if (compilationWithAnalyzers == null)
                continue;

            var diagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync(cancellationToken);
            allDiagnostics.AddRange(diagnostics.Select(diagnostic => new DiagnosticHolder(diagnostic, project)));
        }

        var diagnosticsByDocument = allDiagnostics.Where(d => !string.IsNullOrEmpty(d.FilePath)).GroupBy(d => d.FilePath);
        foreach (var diagnostics in diagnosticsByDocument) {
            workspaceDiagnostics[FileSystemExtensions.NormalizePath(diagnostics.Key!)] = diagnostics;
            DiagnosticsChanged?.Invoke(this, new DiagnosticsCollectionChangedEventArgs(diagnostics.Key!, diagnostics.Select(d => d.Diagnostic)));
        }

        CurrentSessionLogger.Debug($"[{cancellationToken.GetHashCode()}]: Diagnostics finished");
    }

    private async Task<CompilationWithAnalyzers?> CompileWithAnalyzersAsync(Project project, CancellationToken cancellationToken) {
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null)
            return null;

        var diagnosticAnalyzers = diagnosticAnalyzersLoader.GetComponents(project);
        var compilationWithAnalyzers = compilation.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions);
        return compilationWithAnalyzers;
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