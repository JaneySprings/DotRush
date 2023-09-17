using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Extensions;

public static class DiagnosticsConverter {
    public static IEnumerable<ProtocolModels.Diagnostic> ToServerDiagnostics(this IEnumerable<SourceDiagnostic> diagnostics) {
        return diagnostics.Select(it => {
            var diagnosticSource = it.InnerDiagnostic.Location.SourceTree?.FilePath;
            return new ProtocolModels.Diagnostic() {
                Message = it.InnerDiagnostic.GetMessage(),
                Range = it.InnerDiagnostic.Location.ToRange(),
                Severity = it.InnerDiagnostic.Severity.ToServerSeverity(),
                Source = it.SourceName ?? diagnosticSource,
                Code = it.InnerDiagnostic.Id,
            };
        });
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

    public static string ToOperationString(this ProjectLoadOperation operation) {
        switch (operation) {
            case ProjectLoadOperation.Evaluate:
                return "Evaluating";
            case ProjectLoadOperation.Build:
                return "Building";
            case ProjectLoadOperation.Resolve:
                return "Resolving";
        }

        return "Loading";
    }
}