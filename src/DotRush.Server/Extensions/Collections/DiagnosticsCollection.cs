using Microsoft.CodeAnalysis;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Extensions;

public class DiagnosticsCollection {
    private Dictionary<string, List<ExtendedDiagnostic>> diagnostics;

    public event EventHandler<DiagnosticsCollectionChangedEventArgs>? DiagnosticsChanged;

    public DiagnosticsCollection() {
        diagnostics = new Dictionary<string, List<ExtendedDiagnostic>>();
    }

    public void OpenDocument(string documentPath) {
        if (LanguageServer.IsSourceCodeDocument(documentPath))
            diagnostics.TryAdd(documentPath, new List<ExtendedDiagnostic>());
    }
    public void CloseDocument(string documentPath) {
        diagnostics.Remove(documentPath);
        var eventArgs = new DiagnosticsCollectionChangedEventArgs(documentPath, Enumerable.Empty<ProtocolModels.Diagnostic>());
        DiagnosticsChanged?.Invoke(this, eventArgs);
    }
    public void AppendDocumentDiagnostics(string? documentPath, IEnumerable<Diagnostic> newDiagnostics, Project source) {
        if (string.IsNullOrEmpty(documentPath))
            return;
    
        diagnostics[documentPath].AddRange(newDiagnostics.Select(d => new ExtendedDiagnostic(d, source)));
        var eventArgs = new DiagnosticsCollectionChangedEventArgs(documentPath, diagnostics[documentPath].ToServerDiagnostics());
        DiagnosticsChanged?.Invoke(this, eventArgs);
    }
    public void ClearDocumentDiagnostics(IEnumerable<string> documentPaths) {
        foreach (var documentPath in documentPaths) {
            if (diagnostics.ContainsKey(documentPath))
                diagnostics[documentPath].Clear();
        }
    }

    public IEnumerable<string> GetOpenedDocuments() {
        return diagnostics.Keys.ToArray();
    }
    public IEnumerable<ExtendedDiagnostic>? GetDiagnostics(string documentPath) {
        if (!diagnostics.ContainsKey(documentPath))
            return null;

        return diagnostics[documentPath];
    }
}

public class ExtendedDiagnostic {
    public Diagnostic InnerDiagnostic { get; private set; }
    public ProjectId SourceId { get; private set; }
    public string SourceName { get; private set; }

    public ExtendedDiagnostic(Diagnostic diagnostic, Project source) {
        InnerDiagnostic = diagnostic;
        SourceName = source.Name;
        SourceId = source.Id;
    }
}

public class DiagnosticsCollectionChangedEventArgs : EventArgs {
    public string DocumentPath { get; private set; }
    public IEnumerable<ProtocolModels.Diagnostic> ServerDiagnostics { get; private set; }

    public DiagnosticsCollectionChangedEventArgs(string documentPath, IEnumerable<ProtocolModels.Diagnostic> serverDiagnostics) {
        DocumentPath = documentPath;
        ServerDiagnostics = serverDiagnostics;
    }
}