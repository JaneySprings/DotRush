using System.Collections.ObjectModel;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

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
        var eventArgs = new DiagnosticsCollectionChangedEventArgs(documentPath, Array.Empty<Diagnostic>(), null);
        DiagnosticsChanged?.Invoke(this, eventArgs);
        CurrentSessionLogger.Debug($"Close document: {documentPath}");
    }
    public void AppendDocumentDiagnostics(string? documentPath, IEnumerable<Diagnostic> newDiagnostics, Project source) {
        if (string.IsNullOrEmpty(documentPath))
            return;

        foreach (var diagnostic in newDiagnostics)
            workspaceDiagnostics[documentPath].TryAdd(diagnostic.GetUniqueCode(), diagnostic);

        var eventArgs = new DiagnosticsCollectionChangedEventArgs(documentPath, workspaceDiagnostics[documentPath].Values.ToArray(), source);
        DiagnosticsChanged?.Invoke(this, eventArgs);
    }
    public void ClearDocumentDiagnostics(IEnumerable<string> documentPaths) {
        foreach (var documentPath in documentPaths) {
            if (workspaceDiagnostics.TryGetValue(documentPath, out Dictionary<int, Diagnostic>? value))
                value.Clear();
        }
    }
    public IEnumerable<string> GetOpenedDocuments() {
        return workspaceDiagnostics.Keys.ToArray();
    }
    public IEnumerable<Diagnostic>? GetDiagnostics(string documentPath) {
        if (!workspaceDiagnostics.TryGetValue(documentPath, out Dictionary<int, Diagnostic>? value))
            return null;
        return value.Values;
    }
    public IEnumerable<CodeFixProvider> GetCodeFixProvidersForDiagnosticId(string diagnosticId, Project project) {
        return CodeFixProvidersLoader.GetComponents(project).Where(x => x.FixableDiagnosticIds.CanFixDiagnostic(diagnosticId));
    }
}