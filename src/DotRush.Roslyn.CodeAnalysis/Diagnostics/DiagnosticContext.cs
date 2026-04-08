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
    public AnalysisScope Scope { get; }

    public string? FilePath => Diagnostic.Location.SourceTree?.FilePath;
    public string Id => Diagnostic.Id;
    public TextSpan Span => Diagnostic.Location.SourceSpan;

    public DiagnosticContext(Diagnostic diagnostic, Document document, AnalysisScope scope) : this(diagnostic, document.Project.Name, scope) {
        Document = document;
    }
    public DiagnosticContext(Diagnostic diagnostic, Project project, AnalysisScope scope) : this(diagnostic, project.Name, scope) {
        Document = project.GetDocumentsWithFilePath(FilePath).FirstOrDefault();
    }
    private DiagnosticContext(Diagnostic diagnostic, string sourceName, AnalysisScope scope) {
        Diagnostic = diagnostic;
        SourceName = sourceName;
        Scope = scope;
    }

    public string GetSubject() {
        var message = Diagnostic.GetMessage();
        if (string.IsNullOrEmpty(message))
            return $"Missing subject for {Diagnostic.Id}";

        return message;
    }

    private string GetDebuggerDisplay() {
        return $"{SourceName}: {Diagnostic.ToString()}";
    }
}