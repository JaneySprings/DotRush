using Microsoft.CodeAnalysis;
using CADiagnostic = Microsoft.CodeAnalysis.Diagnostic;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

public class Diagnostic {
    public Project Source { get; }
    public CADiagnostic InnerDiagnostic { get; }

    public Diagnostic(CADiagnostic diagnostic, Project source) {
        Source = source;
        InnerDiagnostic = diagnostic;
    }
}