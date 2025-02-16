using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

public class DiagnosticContext {
    public Diagnostic Diagnostic { get; private set; }
    public Project RelatedProject { get; private set; }

    public string? FilePath => Diagnostic.Location.SourceTree?.FilePath;
    public string Source => RelatedProject.Name;

    public DiagnosticContext(Diagnostic diagnostic, Project relatedProject) {
        Diagnostic = diagnostic;
        RelatedProject = relatedProject;
    }

    public string ToDisplayString() {
        var span = $"{Diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}:{Diagnostic.Location.GetLineSpan().StartLinePosition.Character + 1}";
        var sourcePath = Diagnostic.Location.SourceTree?.FilePath ?? string.Empty;
        return $"{sourcePath}({span})[{RelatedProject.Name}]: {Diagnostic.Severity} {Diagnostic.Id}: {Diagnostic.GetSubject()}";
    }
    public override int GetHashCode() {
        return this.ToDisplayString().GetHashCode();
    }
    public override bool Equals(object? obj) {
        if (obj is DiagnosticContext context)
            return context.GetHashCode() == GetHashCode();

        return false;
    }
}