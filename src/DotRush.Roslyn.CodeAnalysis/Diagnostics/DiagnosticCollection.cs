using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

public class DiagnosticCollection {
    private readonly Dictionary<string, HashSet<DiagnosticContext>> workspaceDiagnostics;
    private string collectionToken = string.Empty;

    public DiagnosticCollection() {
        workspaceDiagnostics = new Dictionary<string, HashSet<DiagnosticContext>>();
        collectionToken = GenerateNewCollectionToken();
    }

    public void AddDiagnostics(IEnumerable<DiagnosticContext> diagnostics, bool overwrite = false) {
        var diagnosticByFile = diagnostics
            .Where(c => !string.IsNullOrEmpty(c.FilePath))
            .GroupBy(c => c.FilePath!)
            .ToArray();

        foreach (var diagnosticsGroup in diagnosticByFile) {
            if (!workspaceDiagnostics.TryGetValue(diagnosticsGroup.Key, out HashSet<DiagnosticContext>? container)) {
                container = new HashSet<DiagnosticContext>();
                workspaceDiagnostics[diagnosticsGroup.Key] = container;
            }

            if (overwrite)
                container.Clear();

            container.UnionWith(diagnosticsGroup);
        }

        collectionToken = GenerateNewCollectionToken();
    }
    public ReadOnlyCollection<DiagnosticContext> GetDiagnostics() {
        return workspaceDiagnostics.Values.SelectMany(c => c).ToList().AsReadOnly();
    }
    public DiagnosticContext? GetById(int diagnosticId) {
        return workspaceDiagnostics.Values.SelectMany(c => c).FirstOrDefault(c => c.GetHashCode() == diagnosticId);
    }
    public string GetCollectionToken() {
        return collectionToken;
    }
    public void Clear() {
        workspaceDiagnostics.Clear();
    }

    private string GenerateNewCollectionToken() {
        collectionToken = Guid.NewGuid().ToString();
        return collectionToken;
    }
}