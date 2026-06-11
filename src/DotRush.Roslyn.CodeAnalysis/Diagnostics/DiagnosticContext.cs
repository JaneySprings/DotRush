using System.Diagnostics;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

public class CompilerDiagnosticContext : DiagnosticContext {
    public CompilerDiagnosticContext(Diagnostic diagnostic, Document document, AnalysisScope scope) : base(diagnostic, document, scope) { }
    public CompilerDiagnosticContext(Diagnostic diagnostic, Project project, AnalysisScope scope) : base(diagnostic, project, scope) { }
}
public class AnalyzerDiagnosticContext : DiagnosticContext {
    public AnalyzerDiagnosticContext(Diagnostic diagnostic, Document document, AnalysisScope scope) : base(diagnostic, document, scope) { }
    public AnalyzerDiagnosticContext(Diagnostic diagnostic, Project project, AnalysisScope scope) : base(diagnostic, project, scope) { }
}

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public abstract class DiagnosticContext {
    public Diagnostic Diagnostic { get; }
    public Document? Document { get; } //TODO: Potential memory leak
    public string SourceName { get; }
    public AnalysisScope Scope { get; }

    public string? FilePath => Diagnostic.Location.SourceTree?.FilePath;
    public string Id => Diagnostic.Id;
    public TextSpan Span => Diagnostic.Location.SourceSpan;

    protected DiagnosticContext(Diagnostic diagnostic, Document document, AnalysisScope scope) : this(diagnostic, document.Project.Name, scope) {
        Document = document;
    }
    protected DiagnosticContext(Diagnostic diagnostic, Project project, AnalysisScope scope) : this(diagnostic, project.Name, scope) {
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

        // 尝试获取中文翻译，命中则返回中文消息，否则回退到分析器原始消息
        if (DiagnosticTranslations.TryGet(Diagnostic.Id, out var translatedMessage))
            return translatedMessage;

        return message;
    }

    private string GetDebuggerDisplay() {
        return $"{SourceName}: {Diagnostic.ToString()}";
    }
}