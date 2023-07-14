using DotRush.Server.Containers;
using Microsoft.CodeAnalysis;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Extensions;

public static class DiagnosticsConverter {
    public static List<ProtocolModels.Diagnostic> ToServerDiagnostics(this IEnumerable<SourceDiagnostic> diagnostics) {
        var result = new List<ProtocolModels.Diagnostic>();
        foreach (var diagnostic in diagnostics) {
            if (diagnostic.InnerDiagnostic.Location.Kind != LocationKind.SourceFile)
                continue;

            var diagnosticSource = diagnostic.InnerDiagnostic.Location.SourceTree?.FilePath;

            // TODO: This is a temp hack, we should check relative path to the project root
            if (diagnosticSource?.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") == true ||
                diagnosticSource?.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") == true)
                continue;

            if (!Path.Exists(diagnosticSource))
                continue;

            result.Add(new ProtocolModels.Diagnostic() {
                Message = diagnostic.InnerDiagnostic.GetMessage(),
                Range = diagnostic.InnerDiagnostic.Location.GetLineSpan().Span.ToRange(),
                Severity = diagnostic.InnerDiagnostic.Severity.ToServerSeverity(),
                Source = diagnostic.Source ?? diagnosticSource,
                Code = diagnostic.InnerDiagnostic.Id,
            });
        }

        return result;
    }

    public static ProtocolModels.DiagnosticSeverity ToServerSeverity(this DiagnosticSeverity severity) {
        switch (severity) {
            case DiagnosticSeverity.Error:
                return ProtocolModels.DiagnosticSeverity.Error;
            case DiagnosticSeverity.Warning:
                return ProtocolModels.DiagnosticSeverity.Warning;
            case DiagnosticSeverity.Info:
                return ProtocolModels.DiagnosticSeverity.Information;
            default:
                return ProtocolModels.DiagnosticSeverity.Hint;
        }
    }
}