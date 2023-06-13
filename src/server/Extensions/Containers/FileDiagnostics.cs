using Microsoft.CodeAnalysis;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Extensions;

public class FileDiagnostics {
    public IEnumerable<SourceDiagnostic> SyntaxDiagnostics { get; private set; }
    public IEnumerable<SourceDiagnostic> AnalyzerDiagnostics { get; private set; }

    public FileDiagnostics() {
        SyntaxDiagnostics = Enumerable.Empty<SourceDiagnostic>();
        AnalyzerDiagnostics = Enumerable.Empty<SourceDiagnostic>();
    }

    public void SetSyntaxDiagnostics(IEnumerable<Diagnostic> diagnostics, Project project) {
        SyntaxDiagnostics = diagnostics.Select(d => new SourceDiagnostic(d, project.Name));
    }
    public void SetAnalyzerDiagnostics(IEnumerable<Diagnostic> diagnostics, Project project) {
        AnalyzerDiagnostics = diagnostics.Select(d => new SourceDiagnostic(d, project.Name));
    }
    public void AddSyntaxDiagnostics(IEnumerable<Diagnostic> diagnostics, Project project) {
        SyntaxDiagnostics = SyntaxDiagnostics.Concat(diagnostics.Select(d => new SourceDiagnostic(d, project.Name)));
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
}
