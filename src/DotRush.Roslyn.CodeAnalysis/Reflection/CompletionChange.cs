using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.Completion;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public class InternalCompletionChange {
    internal static readonly PropertyInfo? propertiesProperty;

    public const string SnippetTextKey = "LSPSnippet";

    static InternalCompletionChange() {
        var completionChangeType = typeof(CompletionChange);
        propertiesProperty = completionChangeType?.GetProperty("Properties", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    public static string? GetProperty(CompletionChange change, string propertyName) {
        if (propertiesProperty == null || propertiesProperty.GetValue(change) is not ImmutableDictionary<string, string> dictionary)
            return null;

        if (dictionary.TryGetValue(propertyName, out var value))
            return value;
        return null;
    }
}