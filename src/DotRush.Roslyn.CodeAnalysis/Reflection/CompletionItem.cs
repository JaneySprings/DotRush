using System.Reflection;
using Microsoft.CodeAnalysis.Completion;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public class InternalCompletionItem {
    internal static readonly PropertyInfo? providerNameProperty;

    static InternalCompletionItem() {
        var completionItemType = typeof(CompletionItem);
        providerNameProperty = completionItemType?.GetProperty("ProviderName", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    public static string? GetProviderName(CompletionItem item) {
        if (providerNameProperty == null || item == null)
            return null;

        return providerNameProperty.GetValue(item) as string;
    }
}