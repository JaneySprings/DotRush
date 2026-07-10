using System.Reflection;
using Microsoft.CodeAnalysis.Completion;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public class InternalCompletionItem {
    internal static readonly PropertyInfo? providerNameProperty;
    internal static readonly PropertyInfo? flagsProperty;

    public const int FlagNone = 0x0;
    public const int FlagCached = 0x1;
    public const int FlagExpanded = 0x2; // item should be shown only when expanded items is requested.

    static InternalCompletionItem() {
        var completionItemType = typeof(CompletionItem);
        providerNameProperty = completionItemType?.GetProperty("ProviderName", BindingFlags.NonPublic | BindingFlags.Instance);
        flagsProperty = completionItemType?.GetProperty("Flags", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    public static string? GetProviderName(CompletionItem item) {
        if (providerNameProperty == null || item == null)
            return null;

        return providerNameProperty.GetValue(item) as string;
    }
    public static int GetFlags(CompletionItem item) {
        if (flagsProperty == null || item == null)
            return FlagNone;

        var result = flagsProperty.GetValue(item);
        if (result == null)
            return FlagNone;

        return Convert.ToInt32(result);
    }
}