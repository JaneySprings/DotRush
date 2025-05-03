using System.Reflection;
using Microsoft.CodeAnalysis.Completion;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public static class InternalCompletionOptions {
    internal static readonly Type? completionOptionsType;
    internal static readonly PropertyInfo? showItemsFromUnimportedNamespacesProperty;
    internal static readonly PropertyInfo? targetTypedCompletionFilterProperty;

    static InternalCompletionOptions() {
        completionOptionsType = typeof(CompletionService).Assembly.GetType("Microsoft.CodeAnalysis.Completion.CompletionOptions");
        showItemsFromUnimportedNamespacesProperty = completionOptionsType?.GetProperty("ShowItemsFromUnimportedNamespaces");
        targetTypedCompletionFilterProperty = completionOptionsType?.GetProperty("TargetTypedCompletionFilter");
    }

    public static object? CreateNew() {
        if (completionOptionsType == null)
            return null;

        return Activator.CreateInstance(completionOptionsType);
    }
    public static void AssignValues(object target, bool showItemsFromUnimportedNamespaces, bool targetTypedCompletionFilter) {
        if (showItemsFromUnimportedNamespacesProperty != null)
            showItemsFromUnimportedNamespacesProperty.SetValue(target, showItemsFromUnimportedNamespaces);
        if (targetTypedCompletionFilterProperty != null)
            targetTypedCompletionFilterProperty.SetValue(target, targetTypedCompletionFilter);
    }
}