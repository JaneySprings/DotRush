using Microsoft.CodeAnalysis;
using Protocol = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Extensions;

public static class DiagnosticsExtensions {
    public static IEnumerable<Protocol.Diagnostic> ToServerDiagnostics(this IEnumerable<ExtendedDiagnostic> diagnostics) {
        return diagnostics.Select(it => {
            var diagnosticSource = it.InnerDiagnostic.Location.SourceTree?.FilePath;
            return new Protocol.Diagnostic() {
                Message = it.InnerDiagnostic.GetSubject(),
                Range = it.InnerDiagnostic.Location.ToRange(),
                Severity = it.InnerDiagnostic.Severity.ToServerSeverity(),
                Source = it.SourceName ?? diagnosticSource,
                Code = it.InnerDiagnostic.Id,
                Data = it.GetHashCode(),
            };
        });
    }

    public static Protocol.DiagnosticSeverity ToServerSeverity(this DiagnosticSeverity severity) {
        switch (severity) {
            case DiagnosticSeverity.Error:
                return Protocol.DiagnosticSeverity.Error;
            case DiagnosticSeverity.Warning:
                return Protocol.DiagnosticSeverity.Warning;
            case DiagnosticSeverity.Info:
                return Protocol.DiagnosticSeverity.Information;
            default:
                return Protocol.DiagnosticSeverity.Hint;
        }
    }
    public static Protocol.DiagnosticSeverity ToServerSeverity(this WorkspaceDiagnosticKind kind) {
        switch (kind) {
            case WorkspaceDiagnosticKind.Failure:
                return Protocol.DiagnosticSeverity.Error;
            case WorkspaceDiagnosticKind.Warning:
                return Protocol.DiagnosticSeverity.Warning;
            default:
                return Protocol.DiagnosticSeverity.Information;
        }
    }

    public static Protocol.Diagnostic UpdateSource(this Protocol.Diagnostic diagnostic, string path) {
        return new Protocol.Diagnostic() {
            Message = diagnostic.Message,
            Range = diagnostic.Range,
            Severity = diagnostic.Severity,
            Source = path,
            Code = diagnostic.Code,
        };
    }

    public static Protocol.Diagnostic ToServerDiagnostic(this WorkspaceDiagnostic diagnostic) {
        return new Protocol.Diagnostic() {
            Message = diagnostic.Message,
            Severity = diagnostic.Kind.ToServerSeverity(),
            Range = new Protocol.Range() {
                Start = new Protocol.Position(0, 0),
                End = new Protocol.Position(0, 0)
            },
        };
    }

    public static string GetSubject(this Diagnostic diagnostic) {
        var message = diagnostic.GetMessage();
        if (string.IsNullOrEmpty(message))
            return $"Missing subject for {diagnostic.Id}";

        return message;
    }
}