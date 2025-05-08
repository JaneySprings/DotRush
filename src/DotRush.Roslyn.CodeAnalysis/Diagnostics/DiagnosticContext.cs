using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class DiagnosticContext {
    public Diagnostic Diagnostic { get; }
    public ProjectId SourceId { get; }
    public string SourceName { get; }
    public bool IsAnalyzerDiagnostic { get; }

    public string? FilePath => Diagnostic.Location.SourceTree?.FilePath;
    public TextSpan Span => Diagnostic.Location.SourceSpan;

    public DiagnosticContext(Diagnostic diagnostic, Project relatedProject, bool isAnalyzerDiagnostic = false) {
        Diagnostic = diagnostic;
        SourceId = relatedProject.Id;
        SourceName = relatedProject.Name;
        IsAnalyzerDiagnostic = isAnalyzerDiagnostic;
    }

    private string GetDebuggerDisplay() {
        return $"{SourceName}: {Diagnostic.ToString()}";
    }
}