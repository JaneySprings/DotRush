using System.Collections.ObjectModel;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using FileSystemExtensions = DotRush.Roslyn.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.CodeAnalysis;

public class CompilationHost {
    private readonly DiagnosticAnalyzersLoader diagnosticAnalyzersLoader = new DiagnosticAnalyzersLoader();
    private readonly Dictionary<string, HashSet<DiagnosticHolder>> workspaceDiagnostics = new Dictionary<string, HashSet<DiagnosticHolder>>();

    public event EventHandler<DiagnosticsCollectionChangedEventArgs>? DiagnosticsChanged;

    public IEnumerable<Diagnostic>? GetDiagnostics(string documentPath) {
        documentPath = FileSystemExtensions.NormalizePath(documentPath);
        if (!workspaceDiagnostics.TryGetValue(documentPath, out HashSet<DiagnosticHolder>? container))
            return null;
        return container.Select(d => d.Diagnostic);
    }
    public DiagnosticHolder? GetDiagnosticById(string documentPath, int diagnosticId) {
        documentPath = FileSystemExtensions.NormalizePath(documentPath);
        if (!workspaceDiagnostics.TryGetValue(documentPath, out HashSet<DiagnosticHolder>? container))
            return null;

        return container.FirstOrDefault(d => d.Id == diagnosticId);
    }


    public async Task DiagnoseAsync(IEnumerable<Project?> projects, bool useRoslynAnalyzers, CancellationToken cancellationToken) {
        workspaceDiagnostics.Clear(); // TODO: You cannot use code actions while diagnostics are being cleared
        bool shouldSkipAnalyzers = false;
        foreach (var project in projects) {
            if (project == null)
                continue;

            foreach (var document in project.Documents) {
                if (document.FilePath != null)
                    workspaceDiagnostics.TryAdd(FileSystemExtensions.NormalizePath(document.FilePath), new HashSet<DiagnosticHolder>());
            }

            var compilation = await CompileAsync(project, cancellationToken).ConfigureAwait(false);
            if (compilation != null) {
                var diagnostics = compilation.GetDiagnostics(cancellationToken);
                var diagnosticsGroups = project.Documents.Where(d => d.FilePath != null).ToDictionary(
                    d => d.FilePath!,
                    d => diagnostics.Where(diagnostic => diagnostic.Location.SourceTree?.FilePath == d.FilePath)
                );
                foreach (var group in diagnosticsGroups) {
                    var groupKey = FileSystemExtensions.NormalizePath(group.Key);
                    foreach (var diagnostic in group.Value)
                        workspaceDiagnostics[groupKey].Add(new DiagnosticHolder(diagnostic, project));
                    DiagnosticsChanged?.Invoke(this, new DiagnosticsCollectionChangedEventArgs(group.Key, workspaceDiagnostics[groupKey].Select(d => d.Diagnostic)));
                }
            }

            if (useRoslynAnalyzers && !shouldSkipAnalyzers) {
                var compilationWithAnalyzers = await CompileWithAnalyzersAsync(project, compilation, cancellationToken).ConfigureAwait(false);
                if (compilationWithAnalyzers != null) {
                    var result = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
                    var diagnostics = result.Where(d => d.Location.SourceTree?.FilePath != null);
                    var diagnosticsGroups = project.Documents.Where(d => d.FilePath != null).ToDictionary(
                        d => d.FilePath!,
                        d => diagnostics.Where(diagnostic => diagnostic.Location.SourceTree?.FilePath == d.FilePath)
                    );
                    foreach (var group in diagnosticsGroups) {
                        var groupKey = FileSystemExtensions.NormalizePath(group.Key);
                        foreach (var diagnostic in group.Value)
                            workspaceDiagnostics[groupKey].Add(new DiagnosticHolder(diagnostic, project));
                        DiagnosticsChanged?.Invoke(this, new DiagnosticsCollectionChangedEventArgs(group.Key, workspaceDiagnostics[groupKey].Select(d => d.Diagnostic)));
                    }
                }
                shouldSkipAnalyzers = true;
            }
        }
        CurrentSessionLogger.Debug("CompilationHost: Diagnostics finished");
    }
    private async Task<Compilation?> CompileAsync(Project project, CancellationToken cancellationToken) {
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        return compilation;
    }
    private async Task<CompilationWithAnalyzers?> CompileWithAnalyzersAsync(Project project, Compilation? compilation, CancellationToken cancellationToken) {
        compilation ??= await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
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