using System.Collections.ObjectModel;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using FileSystemExtensions = DotRush.Roslyn.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.CodeAnalysis;

public class CompilationHost {
    private readonly DiagnosticAnalyzersLoader diagnosticAnalyzersLoader = new DiagnosticAnalyzersLoader();
    private readonly Dictionary<string, List<DiagnosticHolder>> workspaceDiagnostics = new Dictionary<string, List<DiagnosticHolder>>();

    public event EventHandler<DiagnosticsCollectionChangedEventArgs>? DiagnosticsChanged;

    public void OpenDocument(string documentPath) {
        if (!LanguageExtensions.IsSourceCodeDocument(documentPath))
            return;
        if (workspaceDiagnostics.TryAdd(documentPath, new List<DiagnosticHolder>()))
            CurrentSessionLogger.Debug($"Open document: {documentPath}");
    }
    public void CloseDocument(string documentPath) {
        workspaceDiagnostics.Remove(documentPath);
        var eventArgs = new DiagnosticsCollectionChangedEventArgs(documentPath, Array.Empty<Diagnostic>());
        DiagnosticsChanged?.Invoke(this, eventArgs);
        CurrentSessionLogger.Debug($"Close document: {documentPath}");
    }

    private void AddDocumentDiagnostics(string documentPath, IEnumerable<Diagnostic> newDiagnostics, ProjectId projectId) {
        if (!workspaceDiagnostics.TryGetValue(documentPath, out List<DiagnosticHolder>? container))
            return;

        container.AddRange(newDiagnostics.Select(d => new DiagnosticHolder(d, projectId)));
        DiagnosticsChanged?.Invoke(this, new DiagnosticsCollectionChangedEventArgs(documentPath, GetDiagnostics(documentPath)!));
    }
    private void RemoveDocumentDiagnostics(string documentPath) {
        if (!workspaceDiagnostics.TryGetValue(documentPath, out List<DiagnosticHolder>? container))
            return;

        container.Clear();
    }

    public IEnumerable<Diagnostic>? GetDiagnostics(string documentPath) {
        if (!workspaceDiagnostics.TryGetValue(documentPath, out List<DiagnosticHolder>? container))
            return null;
        return container.DistinctBy(d => d.Id).Select(d => d.Diagnostic);
    }
    public (Diagnostic?, ProjectId?) GetDiagnosticById(string documentPath, int diagnosticId) {
        if (!workspaceDiagnostics.TryGetValue(documentPath, out List<DiagnosticHolder>? container))
            return (null, null);

        var diagnostic = container.FirstOrDefault(d => d.Id == diagnosticId);
        return (diagnostic?.Diagnostic, diagnostic?.ProjectId);
    }

    public async Task DiagnoseAsync(Solution solution, bool useRoslynAnalyzers, CancellationToken cancellationToken) {
        var documentPaths = workspaceDiagnostics.Keys.ToArray();
        var projectIds = solution.GetProjectIdsWithDocumentsFilePaths(documentPaths);
        if (projectIds == null)
            return;

        foreach (var documentPath in documentPaths)
            RemoveDocumentDiagnostics(documentPath);

        var analyzerDiagnoseCompleted = false;
        foreach (var projectId in projectIds) {
            var project = solution.GetProject(projectId);
            if (project == null)
                continue;

            var compilation = await DiagnoseAsync(project, documentPaths, cancellationToken).ConfigureAwait(false);
            if (useRoslynAnalyzers && compilation != null && !analyzerDiagnoseCompleted) {
                await AnalyzerDiagnoseAsync(project, documentPaths, compilation, cancellationToken).ConfigureAwait(false);
                analyzerDiagnoseCompleted = true;
            }
        }
    }
    private async Task<Compilation?> DiagnoseAsync(Project project, IEnumerable<string> documentPaths, CancellationToken cancellationToken) {
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null)
            return null;

        var diagnostics = compilation.GetDiagnostics(cancellationToken);
        foreach (var documentPath in documentPaths) {
            var currentFileDiagnostic = diagnostics.Where(d => FileSystemExtensions.PathEquals(d.Location.SourceTree?.FilePath, documentPath));
            AddDocumentDiagnostics(documentPath, currentFileDiagnostic, project.Id);
        }

        return compilation;
    }
    private async Task<CompilationWithAnalyzers?> AnalyzerDiagnoseAsync(Project project, IEnumerable<string> documentPaths, Compilation? compilation, CancellationToken cancellationToken) {
        compilation ??= await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null)
            return null;

        var diagnosticAnalyzers = diagnosticAnalyzersLoader.GetComponents(project);
        var compilationWithAnalyzers = compilation.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var documentPath in documentPaths) {
            var currentFileDiagnostic = diagnostics.Where(d => FileSystemExtensions.PathEquals(d.Location.SourceTree?.FilePath, documentPath));
            AddDocumentDiagnostics(documentPath, currentFileDiagnostic, project.Id);
        }

        return compilationWithAnalyzers;
    }
}

public class DiagnosticsCollectionChangedEventArgs : EventArgs {
    public string DocumentPath { get; private set; }
    public ReadOnlyCollection<Diagnostic> Diagnostics { get; private set; }

    public DiagnosticsCollectionChangedEventArgs(string documentPath, IEnumerable<Diagnostic> diagnostics) {
        DocumentPath = documentPath;
        Diagnostics = new ReadOnlyCollection<Diagnostic>(diagnostics.ToList());
    }
}

internal sealed class DiagnosticHolder {
    public Diagnostic Diagnostic { get; init; }
    public ProjectId ProjectId { get; init; }
    public int Id { get; init; }

    public DiagnosticHolder(Diagnostic diagnostic, ProjectId projectId) {
        Diagnostic = diagnostic;
        ProjectId = projectId;
        Id = diagnostic.GetUniqueId();
    }
}