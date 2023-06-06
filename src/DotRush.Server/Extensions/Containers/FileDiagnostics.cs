using Microsoft.CodeAnalysis;

namespace DotRush.Server.Extensions;

public class FileDiagnostics {
    public IEnumerable<Diagnostic> SyntaxDiagnostics { get; private set; }
    public IEnumerable<Diagnostic> AnalyzerDiagnostics { get; private set; }

    public FileDiagnostics() {
        SyntaxDiagnostics = Enumerable.Empty<Diagnostic>();
        AnalyzerDiagnostics = Enumerable.Empty<Diagnostic>();
    }

    public void SetSyntaxDiagnostics(IEnumerable<Diagnostic> diagnostics) {
        SyntaxDiagnostics = diagnostics;
    }
    public void SetAnalyzerDiagnostics(IEnumerable<Diagnostic> diagnostics) {
        AnalyzerDiagnostics = diagnostics;
    }
    public void AddSyntaxDiagnostics(IEnumerable<Diagnostic> diagnostics) {
        SyntaxDiagnostics = SyntaxDiagnostics.Concat(diagnostics);
    }
    public void AddAnalyzerDiagnostics(IEnumerable<Diagnostic> diagnostics) {
        AnalyzerDiagnostics = AnalyzerDiagnostics.Concat(diagnostics);
    }
    public void ClearAnalyzersDiagnostics() {
        AnalyzerDiagnostics = Enumerable.Empty<Diagnostic>();
    }
    public void ClearSyntaxDiagnostics() {
        SyntaxDiagnostics = Enumerable.Empty<Diagnostic>();
    }

    public IEnumerable<Diagnostic> GetTotalDiagnostics() {
        return SyntaxDiagnostics.Concat(AnalyzerDiagnostics);
    }
}
