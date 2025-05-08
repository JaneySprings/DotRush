using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using Microsoft.CodeAnalysis;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Model.Diagnostic;

namespace DotRush.Roslyn.Server.Extensions;

public static class DiagnosticExtensions {
    // https://github.com/JaneySprings/DotRush/issues/21
    private static readonly string[] priorityDiagnosticIds = {
        // Unnecessary using directive
        "CS8019", "IDE0005"
        // TODO: if needed, add more diagnostic IDs here
    };

    public static ProtocolModels.DiagnosticSeverity ToServerSeverity(this DiagnosticSeverity severity) {
        switch (severity) {
            case DiagnosticSeverity.Error:
                return ProtocolModels.DiagnosticSeverity.Error;
            case DiagnosticSeverity.Warning:
                return ProtocolModels.DiagnosticSeverity.Warning;
            case DiagnosticSeverity.Info:
                return ProtocolModels.DiagnosticSeverity.Information;
            case DiagnosticSeverity.Hidden:
                return ProtocolModels.DiagnosticSeverity.Hint;
            default:
                CurrentSessionLogger.Error($"Unsupported diagnostic severity: {severity}");
                return ProtocolModels.DiagnosticSeverity.Information;

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
            Source = context.SourceName,
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

    public static bool IsHiddenInUI(this DiagnosticContext context) {
        if (context.Diagnostic.Severity == DiagnosticSeverity.Hidden && priorityDiagnosticIds.Contains(context.Diagnostic.Id))
            return false;

        return context.Diagnostic.Severity == DiagnosticSeverity.Hidden;
    }
}