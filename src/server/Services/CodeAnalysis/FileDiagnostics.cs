using Microsoft.CodeAnalysis;
using DotRush.Server.Extensions;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Services;

public class FileDiagnostics {
    public IEnumerable<SourceDiagnostic> syntaxDiagnostics;
    public IEnumerable<SourceDiagnostic> analyzerDiagnostics;

    public FileDiagnostics() {
        syntaxDiagnostics = Enumerable.Empty<SourceDiagnostic>();
        analyzerDiagnostics = Enumerable.Empty<SourceDiagnostic>();
    }

    public void SetSyntaxDiagnostics(IEnumerable<Diagnostic> diagnostics, Project project) {
        syntaxDiagnostics = diagnostics.Select(d => new SourceDiagnostic(d, project));
    }
    public void SetAnalyzerDiagnostics(IEnumerable<Diagnostic> diagnostics, Project project) {
        analyzerDiagnostics = diagnostics.Select(d => new SourceDiagnostic(d, project));
    }
    public void AddSyntaxDiagnostics(IEnumerable<Diagnostic> diagnostics, Project project) {
        syntaxDiagnostics = syntaxDiagnostics.Concat(diagnostics.Select(d => new SourceDiagnostic(d, project)));
    }

    public void ClearAnalyzersDiagnostics() {
        analyzerDiagnostics = Enumerable.Empty<SourceDiagnostic>();
    }
    public void ClearSyntaxDiagnostics() {
        syntaxDiagnostics = Enumerable.Empty<SourceDiagnostic>();
    }

    public IEnumerable<Diagnostic> GetTotalDiagnostics() {
        return syntaxDiagnostics.Concat(analyzerDiagnostics).Select(d => d.InnerDiagnostic);
    }
    public IEnumerable<ProtocolModels.Diagnostic> GetSyntaxServerDiagnostics() {
        return syntaxDiagnostics.ToServerDiagnostics();
    }
    public IEnumerable<ProtocolModels.Diagnostic> GetTotalServerDiagnostics() {
        return syntaxDiagnostics.Concat(analyzerDiagnostics).ToServerDiagnostics();
    }
    public IEnumerable<SourceDiagnostic> GetTotalDiagnosticWrappers() {
        return syntaxDiagnostics.Concat(analyzerDiagnostics);
    }
}

public class SourceDiagnostic {
    public Diagnostic InnerDiagnostic { get; private set; }
    public ProjectId SourceId { get; private set; }
    public string SourceName { get; private set; }

    public SourceDiagnostic(Diagnostic diagnostic, Project source) {
        InnerDiagnostic = diagnostic;
        SourceName = source.Name;
        SourceId = source.Id;
    }
}