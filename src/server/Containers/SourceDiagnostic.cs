using Microsoft.CodeAnalysis;

namespace DotRush.Server.Containers;

public class SourceDiagnostic {
    public Diagnostic InnerDiagnostic { get; private set; }
    public string Source { get; private set; }

    public SourceDiagnostic(Diagnostic diagnostic, string source) {
        InnerDiagnostic = diagnostic;
        Source = source;
    }
}