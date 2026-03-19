using Microsoft.CodeAnalysis.CodeActions;

namespace DotRush.Roslyn.CodeAnalysis.Extensions;

public static class CodeActionExtensions {
    public static int GetUniqueId(this CodeAction codeAction) {
        var id = codeAction.EquivalenceKey ?? codeAction.Title;
        return id.GetHashCode();
    }
}