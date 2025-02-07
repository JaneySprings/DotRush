using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

public class DiagnosticContext {
    private readonly Diagnostic diagnostic;
    private readonly Project relatedProject;

    public string? FilePath => diagnostic.Location.SourceTree?.FilePath;
    public string Source => relatedProject.Name;
    public Diagnostic Diagnostic => diagnostic;

    public DiagnosticContext(Diagnostic diagnostic, Project relatedProject) {
        this.diagnostic = diagnostic;
        this.relatedProject = relatedProject;
    }

    public string ToDisplayString() {
        var span = $"{diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}:{diagnostic.Location.GetLineSpan().StartLinePosition.Character + 1}";
        var sourcePath = diagnostic.Location.SourceTree?.FilePath ?? string.Empty;
        return $"{sourcePath}({span})[{relatedProject.Name}]: {diagnostic.Severity} {diagnostic.Id}: {diagnostic.GetSubject()}";
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