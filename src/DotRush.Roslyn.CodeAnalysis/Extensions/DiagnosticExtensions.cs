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
    public static bool CanFixDiagnostic(this ImmutableArray<string> array, string item) {
        if (item == "CS8019")
            return array.Contains("RemoveUnnecessaryImportsFixable");

        return array.Contains(item);
    }
}