using System.Diagnostics;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class DiagnosticContext {
    public Diagnostic Diagnostic { get; }
    public Document? Document { get; } //TODO: Potential memory leak
    public string SourceName { get; }

    public string? FilePath => Diagnostic.Location.SourceTree?.FilePath;
    public TextSpan Span => Diagnostic.Location.SourceSpan;

    public DiagnosticContext(Diagnostic diagnostic, Document document) {
        Diagnostic = diagnostic;
        SourceName = document.Project.Name;
        Document = document;
    }
    public DiagnosticContext(Diagnostic diagnostic, Project project) {
        Diagnostic = diagnostic;
        SourceName = project.Name;
        Document = project.GetDocumentWithFilePath(FilePath).FirstOrDefault();
    }

    private string GetDebuggerDisplay() {
        return $"{SourceName}: {Diagnostic.ToString()}";
    }
}