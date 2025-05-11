using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

public class DiagnosticCollection {
    private Dictionary<string, List<DiagnosticContext>> workspaceDiagnostics;
    private Dictionary<string, List<DiagnosticContext>>? tempWorkspaceDiagnostics;
    private readonly object lockObject;

    public DiagnosticCollection() {
        workspaceDiagnostics = new Dictionary<string, List<DiagnosticContext>>();
        lockObject = new object();
    }

    public void BeginUpdate() {
        if (tempWorkspaceDiagnostics != null)
            throw new InvalidOperationException($"{nameof(EndUpdate)} method must be called before starting a new update.");

        lock (lockObject) {
            tempWorkspaceDiagnostics = new Dictionary<string, List<DiagnosticContext>>(workspaceDiagnostics.Count);
            foreach (var kvp in workspaceDiagnostics) {
                if (kvp.Value.Count > 0)
                    tempWorkspaceDiagnostics[kvp.Key] = new List<DiagnosticContext>();
            }
        }
    }
    public IEnumerable<DiagnosticContext> AddDiagnostics(ProjectId key, IEnumerable<DiagnosticContext> diagnostics) {
        if (tempWorkspaceDiagnostics == null)
            throw new InvalidOperationException($"{nameof(BeginUpdate)} method must be called before adding diagnostics.");

        lock (lockObject) {
            var validDiagnostics = diagnostics.Where(c => !string.IsNullOrEmpty(c.FilePath) && File.Exists(c.FilePath)).ToArray();
            foreach (var diagnosticsGroup in validDiagnostics.GroupBy(c => c.FilePath!)) {
                if (!tempWorkspaceDiagnostics.TryGetValue(diagnosticsGroup.Key, out List<DiagnosticContext>? container)) {
                    container = new List<DiagnosticContext>();
                    tempWorkspaceDiagnostics[diagnosticsGroup.Key] = container;
                }
                container.AddRange(diagnosticsGroup);
            }

            return validDiagnostics;
        }
    }
    public void EndUpdate() {
        if (tempWorkspaceDiagnostics == null)
            throw new InvalidOperationException($"{nameof(BeginUpdate)} method must be called before ending an update.");

        lock (lockObject) {
            workspaceDiagnostics = tempWorkspaceDiagnostics;
            tempWorkspaceDiagnostics = null;
        }
    }

    public ReadOnlyDictionary<string, List<DiagnosticContext>> GetDiagnostics() {
        return new ReadOnlyDictionary<string, List<DiagnosticContext>>(workspaceDiagnostics);
    }
    public ReadOnlyCollection<DiagnosticContext> GetDiagnosticsByDocumentSpan(Document document, TextSpan span) {
        if (string.IsNullOrEmpty(document.FilePath))
            return new List<DiagnosticContext>().AsReadOnly();
        if (workspaceDiagnostics.TryGetValue(document.FilePath, out List<DiagnosticContext>? diagnostics))
            return diagnostics.Where(d => d.Span.IntersectsWith(span)).ToList().AsReadOnly();

        return new List<DiagnosticContext>().AsReadOnly();
    }
}