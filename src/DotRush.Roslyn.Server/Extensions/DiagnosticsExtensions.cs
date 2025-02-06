using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Model.Diagnostic;

namespace DotRush.Roslyn.Server.Extensions;

public static class DiagnosticExtensions {
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
    public static ProtocolModels.DiagnosticSeverity ToServerSeverity(this WorkspaceDiagnosticKind kind) {
        switch (kind) {
            case WorkspaceDiagnosticKind.Failure:
                return ProtocolModels.DiagnosticSeverity.Error;
            case WorkspaceDiagnosticKind.Warning:
                return ProtocolModels.DiagnosticSeverity.Warning;
            default:
                return ProtocolModels.DiagnosticSeverity.Information;
        }
    }
    public static ProtocolModels.Diagnostic ToServerDiagnostic(this Diagnostic diagnostic) {
        return new ProtocolModels.Diagnostic() {
            Code = diagnostic.Id,
            Message = diagnostic.GetSubject(),
            Range = diagnostic.Location.ToRange(),
            Severity = diagnostic.Severity.ToServerSeverity(),
            Data = diagnostic.GetUniqueId(),
        };
    }
    public static ProtocolModels.Diagnostic ToServerDiagnostic(this WorkspaceDiagnostic diagnostic) {
        return new ProtocolModels.Diagnostic() {
            Message = diagnostic.Message,
            Severity = diagnostic.Kind.ToServerSeverity(),
            Range = new DocumentRange() {
                Start = new Position(0, 0),
                End = new Position(0, 0)
            },
        };
    }
}