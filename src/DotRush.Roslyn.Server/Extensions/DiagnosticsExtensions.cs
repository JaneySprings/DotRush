using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Protocol = OmniSharp.Extensions.LanguageServer.Protocol.Models;

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
        return new Protocol.Diagnostic() {
            Code = diagnostic.Id,
            Message = diagnostic.GetSubject(),
            Range = diagnostic.Location.ToRange(),
            Severity = diagnostic.Severity.ToServerSeverity(),
            Data = diagnostic.GetUniqueId(),
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