using System.Collections.ObjectModel;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using CADiagnostic = Microsoft.CodeAnalysis.Diagnostic;
using FileSystemExtensions = DotRush.Roslyn.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

public abstract class DiagnosticHost {
    protected CodeFixProvidersLoader CodeFixProvidersLoader { get; } = new CodeFixProvidersLoader();
    protected DiagnosticAnalyzersLoader DiagnosticAnalyzersLoader { get; } = new DiagnosticAnalyzersLoader();
    private readonly Dictionary<string, Dictionary<int, Diagnostic>> workspaceDiagnostics = new Dictionary<string, Dictionary<int, Diagnostic>>();
    public event EventHandler<DiagnosticsCollectionChangedEventArgs>? DiagnosticsChanged;

    public abstract void Initialize();
    public virtual void OpenDocument(string documentPath) {
        if (!LanguageExtensions.IsSourceCodeDocument(documentPath))
            return;
        if (workspaceDiagnostics.TryAdd(documentPath, new Dictionary<int, Diagnostic>()))
            CurrentSessionLogger.Debug($"Open document: {documentPath}");
    }
    public virtual void CloseDocument(string documentPath) {
        workspaceDiagnostics.Remove(documentPath);
        var eventArgs = new DiagnosticsCollectionChangedEventArgs(documentPath, Array.Empty<Diagnostic>());
        DiagnosticsChanged?.Invoke(this, eventArgs);
        CurrentSessionLogger.Debug($"Close document: {documentPath}");
    }

    protected void AppendDocumentDiagnostics(string? documentPath, IEnumerable<CADiagnostic> newDiagnostics, Project source) {
        if (string.IsNullOrEmpty(documentPath))
            return;

        foreach (var diagnostic in newDiagnostics)
            workspaceDiagnostics[documentPath].TryAdd(diagnostic.GetUniqueId(), new Diagnostic(diagnostic, source));

        var eventArgs = new DiagnosticsCollectionChangedEventArgs(documentPath, workspaceDiagnostics[documentPath].Values.ToArray());
        DiagnosticsChanged?.Invoke(this, eventArgs);
    }
    protected void ClearDocumentDiagnostics(IEnumerable<string> documentPaths) {
        foreach (var documentPath in documentPaths) {
            if (workspaceDiagnostics.TryGetValue(documentPath, out Dictionary<int, Diagnostic>? value))
                value.Clear();
        }
    }
    protected IEnumerable<string> GetOpenedDocuments() {
        return workspaceDiagnostics.Keys.ToArray();
    }

    public IEnumerable<Diagnostic>? GetDiagnostics(string documentPath) {
        if (!workspaceDiagnostics.TryGetValue(documentPath, out Dictionary<int, Diagnostic>? value))
            return null;
        return value.Values;
    }
    public IEnumerable<CodeFixProvider>? GetCodeFixProvidersForDiagnosticId(string? diagnosticId, Project project) {
        if (diagnosticId == null)
            return null;
        return CodeFixProvidersLoader.GetComponents(project).Where(x => x.FixableDiagnosticIds.CanFixDiagnostic(diagnosticId));
    }
    public Diagnostic? GetDiagnosticById(string documentPath, int diagnosticId) {
        return GetDiagnostics(documentPath)?.FirstOrDefault(x => x.InnerDiagnostic.GetUniqueId() == diagnosticId);
    }

    protected async Task<Compilation?> DiagnoseAsync(Project project, IEnumerable<string> documentPaths, CancellationToken cancellationToken) {
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
    protected async Task<CompilationWithAnalyzers?> AnalyzerDiagnoseAsync(Project project, IEnumerable<string> documentPaths, Compilation? compilation, CancellationToken cancellationToken) {
        if (compilation == null)
            compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null)
            return null;

        var diagnosticAnalyzers = DiagnosticAnalyzersLoader.GetComponents(project);
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