using System.Collections.ObjectModel;
using DotRush.Common.Extensions;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

public class DiagnosticCollection {
    private readonly Dictionary<string, HashSet<DiagnosticContext>> workspaceDiagnostics;
    private readonly object lockObject;
    private string collectionToken;

    public DiagnosticCollection() {
        workspaceDiagnostics = new Dictionary<string, HashSet<DiagnosticContext>>();
        lockObject = new object();
        collectionToken = GenerateNewCollectionToken();
    }

    public IEnumerable<DiagnosticContext> AddDiagnostics(IEnumerable<DiagnosticContext> diagnostics) {
        lock (lockObject) {
            var validDiagnostics = diagnostics.Where(c => !string.IsNullOrEmpty(c.FilePath) && File.Exists(c.FilePath)).ToArray();
            foreach (var diagnosticsGroup in validDiagnostics.GroupBy(c => c.FilePath!)) {
                if (!workspaceDiagnostics.TryGetValue(diagnosticsGroup.Key, out HashSet<DiagnosticContext>? container)) {
                    container = new HashSet<DiagnosticContext>();
                    workspaceDiagnostics[diagnosticsGroup.Key] = container;
                }
                container.AddRange(diagnosticsGroup);
            }

            collectionToken = GenerateNewCollectionToken();
            return validDiagnostics;
        }
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
        lock (lockObject) {
            workspaceDiagnostics.Clear();
        }
    }

    private string GenerateNewCollectionToken() {
        collectionToken = Guid.NewGuid().ToString();
        return collectionToken;
    }
}