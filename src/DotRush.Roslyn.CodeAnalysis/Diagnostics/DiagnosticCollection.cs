using System.Collections.ObjectModel;
using DotRush.Common.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

public class DiagnosticCollection {
    private readonly Dictionary<string, List<DiagnosticContext>> workspaceDiagnostics;
    private readonly Dictionary<ProjectId, HashSet<string>> diagnosticRelations;
    private readonly object lockObject;

    public Guid CollectionToken { get; private set; }

    public DiagnosticCollection() {
        workspaceDiagnostics = new Dictionary<string, List<DiagnosticContext>>();
        diagnosticRelations = new Dictionary<ProjectId, HashSet<string>>();
        lockObject = new object();
    }

    public IEnumerable<DiagnosticContext> AddDiagnostics(ProjectId key, IEnumerable<DiagnosticContext> diagnostics) {
        lock (lockObject) {
            if (!diagnosticRelations.TryGetValue(key, out HashSet<string>? relations)) {
                relations = new HashSet<string>();
                diagnosticRelations[key] = relations;
            }

            var validDiagnostics = diagnostics.Where(c => !string.IsNullOrEmpty(c.FilePath) && File.Exists(c.FilePath)).ToArray();
            foreach (var diagnosticsGroup in validDiagnostics.GroupBy(c => c.FilePath!)) {
                if (!workspaceDiagnostics.TryGetValue(diagnosticsGroup.Key, out List<DiagnosticContext>? container)) {
                    container = new List<DiagnosticContext>();
                    workspaceDiagnostics[diagnosticsGroup.Key] = container;
                }
                container.AddRange(diagnosticsGroup);
                relations.Add(diagnosticsGroup.Key);
            }

            Invalidate();
            return validDiagnostics;
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
    public void ClearDiagnostics(Document document, AnalysisScope scope, bool isAnalyzerOnly) {
        if (string.IsNullOrEmpty(document.FilePath))
            return;

        lock (lockObject) {
            switch (scope) {
                case AnalysisScope.None:
                    return;
                case AnalysisScope.Document:
                    if (workspaceDiagnostics.TryGetValue(document.FilePath, out List<DiagnosticContext>? diagnostics))
                        diagnostics.RemoveAll(c => c.IsAnalyzerDiagnostic == isAnalyzerOnly);
                    break;
                case AnalysisScope.Project:
                    if (diagnosticRelations.TryGetValue(document.Project.Id, out HashSet<string>? relations))
                        relations.ForEach(r => workspaceDiagnostics[r].RemoveAll(c => c.IsAnalyzerDiagnostic == isAnalyzerOnly));
                    break;
            }
        }
    }

    private void Invalidate() {
        CollectionToken = Guid.NewGuid();
    }
}