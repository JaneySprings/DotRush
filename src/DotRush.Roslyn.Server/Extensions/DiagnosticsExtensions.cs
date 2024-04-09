using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Protocol = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Diagnostic = DotRush.Roslyn.CodeAnalysis.Diagnostics.Diagnostic;

namespace DotRush.Roslyn.Server.Extensions;

public static class DiagnosticExtensions {
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
    public static Protocol.Diagnostic ToServerDiagnostic(this Diagnostic diagnostic) {
        var diagnosticSource = diagnostic.InnerDiagnostic.Location.SourceTree?.FilePath;
        var sourceName = diagnostic.Source.Name ?? diagnosticSource;
        return new Protocol.Diagnostic() {
            Source = sourceName,
            Code = diagnostic.InnerDiagnostic.Id,
            Message = diagnostic.InnerDiagnostic.GetSubject(),
            Range = diagnostic.InnerDiagnostic.Location.ToRange(),
            Severity = diagnostic.InnerDiagnostic.Severity.ToServerSeverity(),
            Data = diagnostic.InnerDiagnostic.GetUniqueId(),
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
}