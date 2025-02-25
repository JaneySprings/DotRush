using System.Collections.ObjectModel;
using DotRush.Common.Extensions;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

public class DiagnosticCollection {
    private readonly Dictionary<string, HashSet<DiagnosticContext>> workspaceDiagnostics;
    private readonly Dictionary<ProjectId, HashSet<string>> diagnosticRelations;
    private readonly object lockObject;
    private string collectionToken;

    public DiagnosticCollection() {
        workspaceDiagnostics = new Dictionary<string, HashSet<DiagnosticContext>>();
        diagnosticRelations = new Dictionary<ProjectId, HashSet<string>>();
        lockObject = new object();
        collectionToken = GenerateNewCollectionToken();
    }

    public IEnumerable<DiagnosticContext> AddDiagnostics(ProjectId key, IEnumerable<DiagnosticContext> diagnostics) {
        lock (lockObject) {
            if (!diagnosticRelations.TryGetValue(key, out HashSet<string>? relations)) {
                relations = new HashSet<string>();
                diagnosticRelations[key] = relations;
            }

            var validDiagnostics = diagnostics.Where(c => !string.IsNullOrEmpty(c.FilePath) && File.Exists(c.FilePath)).ToArray();
            foreach (var diagnosticsGroup in validDiagnostics.GroupBy(c => c.FilePath!)) {
                if (!workspaceDiagnostics.TryGetValue(diagnosticsGroup.Key, out HashSet<DiagnosticContext>? container)) {
                    container = new HashSet<DiagnosticContext>();
                    workspaceDiagnostics[diagnosticsGroup.Key] = container;
                }
                container.AddRange(diagnosticsGroup);
                relations.Add(diagnosticsGroup.Key);
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
    public void ClearWithKey(ProjectId key) {
        lock (lockObject) {
            if (diagnosticRelations.TryGetValue(key, out HashSet<string>? relations)) {
                relations.ForEach(r => workspaceDiagnostics.Remove(r));
                diagnosticRelations.Remove(key);
            }
        }
    }

    private string GenerateNewCollectionToken() {
        collectionToken = Guid.NewGuid().ToString();
        return collectionToken;
    }
}