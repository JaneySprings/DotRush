using Microsoft.CodeAnalysis;

namespace DotRush.Server.Extensions;

public static class DiagnosticsConverter {
    public static List<LanguageServer.Parameters.TextDocument.Diagnostic> ToServerDiagnostics(this IEnumerable<Diagnostic> diagnostics) {
        var result = new List<LanguageServer.Parameters.TextDocument.Diagnostic>();
        foreach (var diagnostic in diagnostics) {
            if (diagnostic.Location.IsInMetadata || !diagnostic.Location.IsInSource)
                continue;

            var lspdiag = new LanguageServer.Parameters.TextDocument.Diagnostic();
            lspdiag.message = diagnostic.GetMessage();
            lspdiag.severity = diagnostic.Severity.ToServerSeverity();
            lspdiag.source = diagnostic.Location.SourceTree?.FilePath;
            lspdiag.code = diagnostic.Id;

            lspdiag.range = diagnostic.Location.GetLineSpan().Span.ToRange();
            result.Add(lspdiag);
        }

        return result;
    }

    public static LanguageServer.Parameters.TextDocument.DiagnosticSeverity ToServerSeverity(this DiagnosticSeverity severity) {
        switch (severity) {
            case DiagnosticSeverity.Error:
                return LanguageServer.Parameters.TextDocument.DiagnosticSeverity.Error;
            case DiagnosticSeverity.Warning:
                return LanguageServer.Parameters.TextDocument.DiagnosticSeverity.Warning;
            case DiagnosticSeverity.Info:
                return LanguageServer.Parameters.TextDocument.DiagnosticSeverity.Information;
            case DiagnosticSeverity.Hidden:
                return LanguageServer.Parameters.TextDocument.DiagnosticSeverity.Information;
            default:
                return LanguageServer.Parameters.TextDocument.DiagnosticSeverity.Error;
        }
    }
}