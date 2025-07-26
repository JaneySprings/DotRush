using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Navigation.Extensions;

internal static class SymbolExtensions {
    internal static string GetNamedTypeFullName(this ISymbol symbol) {
        var containingType = symbol.GetNamedTypeSymbol();
        var stack = new Stack<string>();
        if (containingType?.MetadataName == null)
            return string.Empty;

        stack.Push(containingType.MetadataName);
        var ns = containingType.ContainingNamespace;
        do {
            stack.Push(ns.Name);
            ns = ns.ContainingNamespace;
        } while (ns != null && !ns.IsGlobalNamespace);

        return string.Join(".", stack);
    }
}