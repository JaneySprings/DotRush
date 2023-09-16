using Microsoft.CodeAnalysis;

namespace DotRush.Server.Extensions;

//https://github.com/OmniSharp/omnisharp-roslyn/blob/master/src/OmniSharp.Roslyn/Extensions/SymbolExtensions.cs
public static class SymbolExtensions {
    public static string GetFullReflectionName(this INamedTypeSymbol containingType) {
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
    
    public static INamedTypeSymbol GetTopLevelContainingNamedType(this ISymbol symbol) {
        // Traverse up until we find a named type that is parented by the namespace
        var topLevelNamedType = symbol;
        while (!SymbolEqualityComparer.Default.Equals(topLevelNamedType.ContainingSymbol, symbol.ContainingNamespace) || topLevelNamedType.Kind != SymbolKind.NamedType) {
            topLevelNamedType = topLevelNamedType.ContainingSymbol;
        }

        return (INamedTypeSymbol)topLevelNamedType;
    }

}