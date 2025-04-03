using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Model.Diagnostic;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;

namespace DotRush.Roslyn.Server.Extensions;

public static class DiagnosticExtensions {
    public static ProtocolModels.DiagnosticSeverity ToServerSeverity(this DiagnosticSeverity severity) {
        switch (severity) {
            case DiagnosticSeverity.Error:
                return ProtocolModels.DiagnosticSeverity.Error;
            case DiagnosticSeverity.Warning:
                return ProtocolModels.DiagnosticSeverity.Warning;
            // https://github.com/JaneySprings/DotRush/issues/21
            // VS doesn't show hidden diagnostics in the UI
            case DiagnosticSeverity.Info:
            case DiagnosticSeverity.Hidden:
                return ProtocolModels.DiagnosticSeverity.Hint;
            default:
                throw new NotImplementedException($"Unsupported diagnostic severity: {severity}");
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
    public static ProtocolModels.Diagnostic ToServerDiagnostic(this DiagnosticContext context) {
        return new ProtocolModels.Diagnostic() {
            Code = context.Diagnostic.Id,
            Message = context.Diagnostic.GetSubject(),
            Range = context.Diagnostic.Location.ToRange(),
            Severity = context.Diagnostic.Severity.ToServerSeverity(),
            Source = context.Source,
            Data = context.GetHashCode(),
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