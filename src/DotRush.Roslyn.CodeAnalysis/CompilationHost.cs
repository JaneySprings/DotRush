using System.Collections.ObjectModel;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using CADiagnostic = Microsoft.CodeAnalysis.Diagnostic;
using Diagnostic = DotRush.Roslyn.CodeAnalysis.Diagnostics.Diagnostic;
using FileSystemExtensions = DotRush.Roslyn.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.CodeAnalysis;

public class CompilationHost {
    private readonly DiagnosticAnalyzersLoader diagnosticAnalyzersLoader = new DiagnosticAnalyzersLoader();
    private readonly Dictionary<string, Dictionary<int, Diagnostic>> workspaceDiagnostics = new Dictionary<string, Dictionary<int, Diagnostic>>();

    public event EventHandler<DiagnosticsCollectionChangedEventArgs>? DiagnosticsChanged;

    public void OpenDocument(string documentPath) {
        if (!LanguageExtensions.IsSourceCodeDocument(documentPath))
            return;
        if (workspaceDiagnostics.TryAdd(documentPath, new Dictionary<int, Diagnostic>()))
            CurrentSessionLogger.Debug($"Open document: {documentPath}");
    }
    public void CloseDocument(string documentPath) {
        workspaceDiagnostics.Remove(documentPath);
        var eventArgs = new DiagnosticsCollectionChangedEventArgs(documentPath, Array.Empty<Diagnostic>());
        DiagnosticsChanged?.Invoke(this, eventArgs);
        CurrentSessionLogger.Debug($"Close document: {documentPath}");
    }
    public IEnumerable<string> GetOpenedDocuments() {
        return workspaceDiagnostics.Keys.ToArray();
    }

    public void AppendDocumentDiagnostics(string? documentPath, IEnumerable<CADiagnostic> newDiagnostics, Project source) {
        if (string.IsNullOrEmpty(documentPath))
            return;

        if (!workspaceDiagnostics.ContainsKey(documentPath))
            workspaceDiagnostics[documentPath] = new Dictionary<int, Diagnostic>();

        foreach (var diagnostic in newDiagnostics)
            workspaceDiagnostics[documentPath].TryAdd(diagnostic.GetUniqueId(), new Diagnostic(diagnostic, source));

        var eventArgs = new DiagnosticsCollectionChangedEventArgs(documentPath, workspaceDiagnostics[documentPath].Values.ToArray());
        DiagnosticsChanged?.Invoke(this, eventArgs);
    }
    public void RemoveDocumentDiagnostics(IEnumerable<string> documentPaths) {
        foreach (var documentPath in documentPaths)
            workspaceDiagnostics.Remove(documentPath);
    }

    public IEnumerable<Diagnostic>? GetDiagnostics(string documentPath) {
        if (!workspaceDiagnostics.TryGetValue(documentPath, out Dictionary<int, Diagnostic>? value))
            return null;
        return value.Values;
    }
    public Diagnostic? GetDiagnosticById(string documentPath, int diagnosticId) {
        return GetDiagnostics(documentPath)?.FirstOrDefault(x => x.InnerDiagnostic.GetUniqueId() == diagnosticId);
    }

    public async Task<Compilation?> DiagnoseAsync(Project project, IEnumerable<string> documentPaths, CancellationToken cancellationToken) {
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null)
            return null;

        var diagnostics = compilation.GetDiagnostics(cancellationToken);
        foreach (var documentPath in documentPaths) {
            var currentFileDiagnostic = diagnostics.Where(d => FileSystemExtensions.PathEquals(d.Location.SourceTree?.FilePath, documentPath));
            AppendDocumentDiagnostics(documentPath, currentFileDiagnostic, project);
        }

        return compilation;
    }
    public async Task<CompilationWithAnalyzers?> AnalyzerDiagnoseAsync(Project project, IEnumerable<string> documentPaths, Compilation? compilation, CancellationToken cancellationToken) {
        if (compilation == null)
            compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null)
            return null;

        var diagnosticAnalyzers = diagnosticAnalyzersLoader.GetComponents(project);
        var compilationWithAnalyzers = compilation.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var documentPath in documentPaths) {
            var currentFileDiagnostic = diagnostics.Where(d => FileSystemExtensions.PathEquals(d.Location.SourceTree?.FilePath, documentPath));
            AppendDocumentDiagnostics(documentPath, currentFileDiagnostic, project);
        }

        return compilationWithAnalyzers;
    }
}

public class DiagnosticsCollectionChangedEventArgs : EventArgs {
    public string DocumentPath { get; private set; }
    public ReadOnlyCollection<Diagnostic> Diagnostics { get; private set; }

    public DiagnosticsCollectionChangedEventArgs(string documentPath, IList<Diagnostic> diagnostics) {
        DocumentPath = documentPath;
        Diagnostics = new ReadOnlyCollection<Diagnostic>(diagnostics);
    }
}