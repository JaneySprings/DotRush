
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.CodeAnalysis.Extensions;

public static class DiagnosticsExtensions {
    public static string GetSubject(this Diagnostic diagnostic) {
        var message = diagnostic.GetMessage();
        if (string.IsNullOrEmpty(message))
            return $"Missing subject for {diagnostic.Id}";

        return message;
    }
    public static string ToDisplayString(this Diagnostic diagnostic) {
        var span = $"{diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}:{diagnostic.Location.GetLineSpan().StartLinePosition.Character + 1}";
        var sourcePath = diagnostic.Location.SourceTree?.FilePath ?? string.Empty;
        return $"{sourcePath}({span}): {diagnostic.Severity} {diagnostic.Id}: {diagnostic.GetSubject()}";
    }
    public static int GetUniqueCode(this Diagnostic diagnostic) {
        return diagnostic.ToDisplayString().GetHashCode();
    }

    public static bool CanFixDiagnostic(this ImmutableArray<string> array, string item) {
        if (item == "CS8019")
            return array.Contains("RemoveUnnecessaryImportsFixable");

        return array.Contains(item);
    }
}