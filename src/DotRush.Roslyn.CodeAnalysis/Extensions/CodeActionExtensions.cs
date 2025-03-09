using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;

namespace DotRush.Roslyn.CodeAnalysis.Extensions;

public static class CodeActionExtensions {
    public static IEnumerable<CodeAction> ToFlattenCodeActions(this CodeAction codeAction) {
        if (codeAction.NestedActions.IsEmpty)
            return new[] { codeAction };

        return codeAction.NestedActions.SelectMany(ToFlattenCodeActions);
    }

    public static int GetUniqueId(this CodeAction codeAction) {
        var id = codeAction.EquivalenceKey ?? codeAction.Title;
        return id.GetHashCode();
    }
}