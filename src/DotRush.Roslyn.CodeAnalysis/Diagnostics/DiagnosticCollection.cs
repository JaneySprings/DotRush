using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

public class DiagnosticCollection {
    private DiagnosticsSnapshot currentSnapshot;
    private DiagnosticsSnapshot? stagingSnapshot;
    private readonly object lockObject;

    public DiagnosticCollection() {
        currentSnapshot = DiagnosticsSnapshot.Empty;
        lockObject = new object();
    }

    public void BeginUpdate() {
        if (stagingSnapshot != null)
            throw new InvalidOperationException($"{nameof(EndUpdate)} method must be called before starting a new update.");

        lock (lockObject)
            stagingSnapshot = currentSnapshot.CreateStaging();
    }
    public IEnumerable<DiagnosticContext> AddDiagnostics(ProjectId key, IEnumerable<DiagnosticContext> diagnostics) {
        if (stagingSnapshot == null)
            throw new InvalidOperationException($"{nameof(BeginUpdate)} method must be called before adding diagnostics.");

        lock (lockObject) {
            var acceptedDiagnostics = new List<DiagnosticContext>();

            foreach (var diagnostic in diagnostics) {
                stagingSnapshot.Add(key, diagnostic);
                acceptedDiagnostics.Add(diagnostic);
            }

            return acceptedDiagnostics;
        }
    }
    public void EndUpdate() {
        if (stagingSnapshot == null)
            throw new InvalidOperationException($"{nameof(BeginUpdate)} method must be called before ending an update.");

        lock (lockObject) {
            currentSnapshot = stagingSnapshot;
            stagingSnapshot = null;
        }
    }

    public ReadOnlyDictionary<string, List<DiagnosticContext>> GetDiagnostics() {
        return new ReadOnlyDictionary<string, List<DiagnosticContext>>(currentSnapshot.ByFile);
    }
    public ReadOnlyCollection<DiagnosticContext> GetDiagnosticsByDocument(Document document) {
        if (string.IsNullOrEmpty(document.FilePath))
            return ReadOnlyCollection<DiagnosticContext>.Empty;
        if (currentSnapshot.ByFile.TryGetValue(document.FilePath, out List<DiagnosticContext>? diagnostics))
            return diagnostics.Where(d => d.ProjectId == document.Project.Id).ToList().AsReadOnly();

        return ReadOnlyCollection<DiagnosticContext>.Empty;
    }
    public ReadOnlyCollection<DiagnosticContext> GetDiagnosticsByProject(Project project) {
        var result = new List<DiagnosticContext>();
        foreach (var document in project.Documents) {
            if (!string.IsNullOrEmpty(document.FilePath) && currentSnapshot.ByFile.TryGetValue(document.FilePath, out var diagnostics))
                result.AddRange(diagnostics.Where(d => d.ProjectId == project.Id));
        }

        if (currentSnapshot.ByProject.TryGetValue(project.Id, out var projectDiagnostics))
            result.AddRange(projectDiagnostics);

        return result.AsReadOnly();
    }
    public ReadOnlyCollection<DiagnosticContext> GetDiagnosticsByDocumentSpan(Document document, TextSpan span) {
        if (string.IsNullOrEmpty(document.FilePath))
            return ReadOnlyCollection<DiagnosticContext>.Empty;
        if (currentSnapshot.ByFile.TryGetValue(document.FilePath, out List<DiagnosticContext>? diagnostics))
            return diagnostics.Where(d => d.ProjectId == document.Project.Id && d.Span.IntersectsWith(span)).ToList().AsReadOnly();

        return ReadOnlyCollection<DiagnosticContext>.Empty;
    }

    private sealed class DiagnosticsSnapshot {
        public static DiagnosticsSnapshot Empty { get; } = new(new Dictionary<string, List<DiagnosticContext>>(), new Dictionary<ProjectId, List<DiagnosticContext>>());

        public DiagnosticsSnapshot(Dictionary<string, List<DiagnosticContext>> byFile, Dictionary<ProjectId, List<DiagnosticContext>> byProject) {
            ByFile = byFile;
            ByProject = byProject;
        }

        public Dictionary<string, List<DiagnosticContext>> ByFile { get; }
        public Dictionary<ProjectId, List<DiagnosticContext>> ByProject { get; }

        public DiagnosticsSnapshot CreateStaging() {
            return new DiagnosticsSnapshot(
                ByFile.Keys.ToDictionary(key => key, _ => new List<DiagnosticContext>()),
                ByProject.Keys.ToDictionary(key => key, _ => new List<DiagnosticContext>())
            );
        }
        public void Add(ProjectId projectId, DiagnosticContext diagnostic) {
            if (!string.IsNullOrEmpty(diagnostic.FilePath) && File.Exists(diagnostic.FilePath)) {
                GetBucket(ByFile, diagnostic.FilePath).Add(diagnostic);
                return;
            }

            GetBucket(ByProject, projectId).Add(diagnostic);
        }

        private static List<DiagnosticContext> GetBucket<TKey>(Dictionary<TKey, List<DiagnosticContext>> source, TKey key) where TKey : notnull {
            if (!source.TryGetValue(key, out List<DiagnosticContext>? bucket)) {
                bucket = new List<DiagnosticContext>();
                source[key] = bucket;
            }

            return bucket;
        }
    }
}
