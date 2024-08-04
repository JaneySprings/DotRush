using DotRush.Roslyn.Common;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Navigation.Extensions;

public static class SymbolExtensions {
    public static string GetNamedTypeFullName(this ISymbol symbol) {
        if (symbol is INamedTypeSymbol namedType)
            return namedType.ToDisplayString(DisplayFormat.Default);

        return symbol.ContainingType.ToDisplayString(DisplayFormat.Default);
    }
}