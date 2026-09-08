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
        lock (lockObject) {
            if (tempWorkspaceDiagnostics != null)
                throw new InvalidOperationException($"{nameof(EndUpdate)} method must be called before starting a new update.");

            tempWorkspaceDiagnostics = new Dictionary<string, List<DiagnosticContext>>(workspaceDiagnostics.Count);
            foreach (var kvp in workspaceDiagnostics) {
                if (kvp.Value.Count > 0)
                    tempWorkspaceDiagnostics[kvp.Key] = new List<DiagnosticContext>();
            }
        }
    }
    public IEnumerable<DiagnosticContext> AddDiagnostics(ProjectId key, IEnumerable<DiagnosticContext> diagnostics) {
        var diagnosticsGroups = diagnostics
            .Where(c => !string.IsNullOrEmpty(c.FilePath))
            .GroupBy(c => c.FilePath!)
            .Where(g => File.Exists(g.Key))
            .ToArray();

        var validDiagnostics = new List<DiagnosticContext>();
        lock (lockObject) {
            if (tempWorkspaceDiagnostics == null)
                throw new InvalidOperationException($"{nameof(BeginUpdate)} method must be called before adding diagnostics.");

            foreach (var diagnosticsGroup in diagnosticsGroups) {
                if (!tempWorkspaceDiagnostics.TryGetValue(diagnosticsGroup.Key, out List<DiagnosticContext>? container)) {
                    container = new List<DiagnosticContext>();
                    tempWorkspaceDiagnostics[diagnosticsGroup.Key] = container;
                }
                container.AddRange(diagnosticsGroup);
                validDiagnostics.AddRange(diagnosticsGroup);
            }
        }

        return validDiagnostics;
    }
    public void EndUpdate() {
        lock (lockObject) {
            if (tempWorkspaceDiagnostics == null)
                throw new InvalidOperationException($"{nameof(BeginUpdate)} method must be called before ending an update.");

            workspaceDiagnostics = tempWorkspaceDiagnostics;
            tempWorkspaceDiagnostics = null;
        }
    }
    public void CancelUpdate() {
        lock (lockObject) {
            tempWorkspaceDiagnostics = null;
        }
    }

    public async Task Execute(Func<Task> handler, CancellationToken cancellationToken) {
        BeginUpdate();
        try {
            await handler.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch {
            CancelUpdate();
            throw;
        }
        EndUpdate();
    }

    public ReadOnlyDictionary<string, List<DiagnosticContext>> GetDiagnostics() {
        return new ReadOnlyDictionary<string, List<DiagnosticContext>>(workspaceDiagnostics);
    }
    public ReadOnlyCollection<DiagnosticContext> GetDiagnosticsByDocument(Document document) {
        if (string.IsNullOrEmpty(document.FilePath))
            return ReadOnlyCollection<DiagnosticContext>.Empty;
        if (workspaceDiagnostics.TryGetValue(document.FilePath, out List<DiagnosticContext>? diagnostics))
            return diagnostics.AsReadOnly();

        return ReadOnlyCollection<DiagnosticContext>.Empty;
    }
    public ReadOnlyCollection<DiagnosticContext> GetDiagnosticsByDocumentSpan(Document document, TextSpan span) {
        if (string.IsNullOrEmpty(document.FilePath))
            return ReadOnlyCollection<DiagnosticContext>.Empty;
        if (workspaceDiagnostics.TryGetValue(document.FilePath, out List<DiagnosticContext>? diagnostics))
            return diagnostics.Where(d => d.Span.IntersectsWith(span)).ToList().AsReadOnly();

        return ReadOnlyCollection<DiagnosticContext>.Empty;
    }
    public ReadOnlyCollection<DiagnosticContext> GetDiagnosticsByProject(Project project) {
        var result = new List<DiagnosticContext>();
        foreach (var document in project.Documents) {
            if (string.IsNullOrEmpty(document.FilePath))
                continue;
            if (workspaceDiagnostics.TryGetValue(document.FilePath, out List<DiagnosticContext>? diagnostics))
                result.AddRange(diagnostics);
        }
        return result.AsReadOnly();
    }
}
