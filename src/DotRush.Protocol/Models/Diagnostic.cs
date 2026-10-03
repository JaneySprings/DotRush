namespace DotRush.Protocol.Models;

public class Diagnostic {
    public DocumentRange Range { get; set; }
    public DiagnosticSeverity? Severity { get; set; }
    public string? Code { get; set; }
    public CodeDescription? CodeDescription { get; set; }
    public string? Source { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<DiagnosticTag>? Tags { get; set; }
}

public record CodeDescription(Uri Href);

public enum DiagnosticSeverity {
    Error = 1,
    Warning = 2,
    Information = 3,
    Hint = 4
}

public enum DiagnosticTag {
    Unnecessary = 1,
    Deprecated = 2
}

public class PublishDiagnosticsParams {
    public DocumentUri Uri { get; set; }
    public int? Version { get; set; }
    public List<Diagnostic> Diagnostics { get; set; } = null!;
}
