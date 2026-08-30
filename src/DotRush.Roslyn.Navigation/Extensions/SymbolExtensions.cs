using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Navigation.Extensions;

internal static class SymbolExtensions {
    // Nested symbols are located inside the decompiled top-level type document
    internal static string GetTopLevelTypeFullName(this ISymbol symbol) {
        var type = symbol.GetNamedTypeSymbol();
        while (type?.ContainingType != null)
            type = type.ContainingType;
        if (type?.MetadataName == null)
            return string.Empty;

        var stack = new Stack<string>();
        stack.Push(type.MetadataName);
        var ns = type.ContainingNamespace;
        while (ns != null && !ns.IsGlobalNamespace) {
            stack.Push(ns.Name);
            ns = ns.ContainingNamespace;
        }

        return string.Join(".", stack);
    }
}
