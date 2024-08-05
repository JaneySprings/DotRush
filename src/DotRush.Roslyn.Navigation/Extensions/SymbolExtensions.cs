using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Navigation.Extensions;

public static class SymbolExtensions {
    public static string GetNamedTypeFullName(this ISymbol symbol) {
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

    public static INamedTypeSymbol GetNamedTypeSymbol(this ISymbol symbol) {
        if (symbol is INamedTypeSymbol namedType)
            return namedType;

        return symbol.ContainingType;
    }
}