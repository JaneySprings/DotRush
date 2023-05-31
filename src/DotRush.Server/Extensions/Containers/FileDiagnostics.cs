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
    public void ClearAnalyzersDiagnostics() {
        AnalyzerDiagnostics = Enumerable.Empty<Diagnostic>();
    }
    public void ClearSyntaxDiagnostics() {
        SyntaxDiagnostics = Enumerable.Empty<Diagnostic>();
    }
    public bool AnalyzersDiagnosticsEquals(IEnumerable<Diagnostic> diagnostics) {
        if (diagnostics.Count() != SyntaxDiagnostics.Count())
            return false;

        foreach (var diagnostic in diagnostics) {
            if (AnalyzerDiagnostics.FirstOrDefault(d => d.Equals(diagnostic)) == null)
                return false;
        }

        return true;
    }
    public bool SyntaxDiagnosticsEquals(IEnumerable<Diagnostic> diagnostics) {
        if (diagnostics.Count() != SyntaxDiagnostics.Count())
            return false;

        foreach (var diagnostic in diagnostics) {
            if (SyntaxDiagnostics.FirstOrDefault(d => d.Equals(diagnostic)) == null)
                return false;
        }

        return true;
    }


    public IEnumerable<Diagnostic> GetTotalDiagnostics() {
        return SyntaxDiagnostics.Concat(AnalyzerDiagnostics);
    }
}
