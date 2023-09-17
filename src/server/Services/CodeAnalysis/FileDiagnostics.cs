using Microsoft.CodeAnalysis;
using DotRush.Server.Extensions;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Services;

public class FileDiagnostics {
    public IEnumerable<SourceDiagnostic> SyntaxDiagnostics { get; private set; }
    public IEnumerable<SourceDiagnostic> AnalyzerDiagnostics { get; private set; }

    public FileDiagnostics() {
        SyntaxDiagnostics = Enumerable.Empty<SourceDiagnostic>();
        AnalyzerDiagnostics = Enumerable.Empty<SourceDiagnostic>();
    }

    public void SetSyntaxDiagnostics(IEnumerable<Diagnostic> diagnostics, Project project) {
        SyntaxDiagnostics = diagnostics.Select(d => new SourceDiagnostic(d, project));
    }
    public void SetAnalyzerDiagnostics(IEnumerable<Diagnostic> diagnostics, Project project) {
        AnalyzerDiagnostics = diagnostics.Select(d => new SourceDiagnostic(d, project));
    }
    public void AddSyntaxDiagnostics(IEnumerable<Diagnostic> diagnostics, Project project) {
        SyntaxDiagnostics = SyntaxDiagnostics.Concat(diagnostics.Select(d => new SourceDiagnostic(d, project)));
    }

    public void ClearAnalyzersDiagnostics() {
        AnalyzerDiagnostics = Enumerable.Empty<SourceDiagnostic>();
    }
    public void ClearSyntaxDiagnostics() {
        SyntaxDiagnostics = Enumerable.Empty<SourceDiagnostic>();
    }

    public IEnumerable<Diagnostic> GetTotalDiagnostics() {
        return SyntaxDiagnostics.Concat(AnalyzerDiagnostics).Select(d => d.InnerDiagnostic);
    }
    public IEnumerable<ProtocolModels.Diagnostic> GetTotalServerDiagnostics() {
        return SyntaxDiagnostics.Concat(AnalyzerDiagnostics).ToServerDiagnostics();
    }
    public IEnumerable<SourceDiagnostic> GetTotalDiagnosticWrappers() {
        return SyntaxDiagnostics.Concat(AnalyzerDiagnostics);
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