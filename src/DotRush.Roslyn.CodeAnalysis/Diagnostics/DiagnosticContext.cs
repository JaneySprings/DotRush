using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

public class DiagnosticContext {
    public Diagnostic Diagnostic { get; private set; }
    public Project RelatedProject { get; private set; }

    public string? FilePath => Diagnostic.Location.SourceTree?.FilePath;
    public TextSpan Span => Diagnostic.Location.SourceSpan;
    public string Source => RelatedProject.Name;

    public DiagnosticContext(Diagnostic diagnostic, Project relatedProject) {
        Diagnostic = diagnostic;
        RelatedProject = relatedProject;
    }
}