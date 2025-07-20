using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.CodeAnalysis.Extensions;
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

    public static ProtocolModels.DiagnosticSeverity ToServerSeverity(this DiagnosticSeverity severity, DiagnosticsFormat format) {
        if (severity == DiagnosticSeverity.Error)
            return ProtocolModels.DiagnosticSeverity.Error;
        if (severity == DiagnosticSeverity.Warning)
            return ProtocolModels.DiagnosticSeverity.Warning;

        if (severity == DiagnosticSeverity.Info && format == DiagnosticsFormat.InfosAsHints)
            return ProtocolModels.DiagnosticSeverity.Hint;

        if (severity == DiagnosticSeverity.Info)
            return ProtocolModels.DiagnosticSeverity.Information;
        if (severity == DiagnosticSeverity.Hidden)
            return ProtocolModels.DiagnosticSeverity.Hint;

        throw new NotImplementedException($"Unknown diagnostic severity: {severity}");
    }
    public static ProtocolModels.Diagnostic ToServerDiagnostic(this DiagnosticContext context, DiagnosticsFormat format) {
        var diagnostic = new ProtocolModels.Diagnostic() {
            Code = context.Diagnostic.Id,
            Message = context.Diagnostic.GetSubject(),
            Range = context.Diagnostic.Location.ToRange(),
            Severity = context.Diagnostic.Severity.ToServerSeverity(format),
            Source = context.SourceName,
        };

        var helpUriString = context.Diagnostic.Descriptor.HelpLinkUri;
        if (!string.IsNullOrEmpty(helpUriString) && Uri.TryCreate(helpUriString, UriKind.Absolute, out Uri? helpUri))
            diagnostic.CodeDescription = new ProtocolModels.CodeDescription(helpUri);

        return diagnostic;
    }

    public static bool IsHiddenInUI(this DiagnosticContext context) {
        if (context.Diagnostic.Severity == DiagnosticSeverity.Hidden && priorityDiagnosticIds.Contains(context.Diagnostic.Id))
            return false;

        return context.Diagnostic.Severity == DiagnosticSeverity.Hidden;
    }
}

public enum DiagnosticsFormat {
    NoHints,
    InfosAsHints,
    AsIs,
}