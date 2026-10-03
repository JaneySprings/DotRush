using DotRush.Common.Extensions;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.CodeAnalysis.Extensions;

public static class SymbolExtensions {
    public static ITypeSymbol? GetTypeSymbol(this ISymbol symbol) {
        if (symbol is ITypeSymbol typeSymbol)
            return typeSymbol;

        if (symbol is ILocalSymbol localSymbol)
            return localSymbol.Type;
        if (symbol is IFieldSymbol fieldSymbol)
            return fieldSymbol.Type;
        if (symbol is IPropertySymbol propertySymbol)
            return propertySymbol.Type;
        if (symbol is IParameterSymbol parameterSymbol)
            return parameterSymbol.Type;
        if (symbol is IMethodSymbol methodSymbol)
            return methodSymbol.ContainingType;

        return null;
    }
    public static INamedTypeSymbol GetNamedTypeSymbol(this ISymbol symbol) {
        if (symbol is INamedTypeSymbol namedType)
            return namedType;

        return symbol.ContainingType;
    }
    public static string GetFullName(this ISymbol symbol) {
        return symbol.ToDisplayString(DisplayFormat.Type);
    }

    public static IEnumerable<ISymbol> GetAllMembers(this INamedTypeSymbol symbol) {
        var members = new List<ISymbol>();
        members.AddRange(symbol.GetMembers());

        while (symbol.BaseType != null) {
            symbol = symbol.BaseType;
            members.AddRange(symbol.GetMembers());
        }

        return members;
    }

    public static bool FuzzySearch(string symbolName, string query) {
        if (symbolName.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        var queryParts = query.SplitByCase();
        bool isMatch = true;
        foreach (var part in queryParts) {
            if (!symbolName.Contains(part, StringComparison.OrdinalIgnoreCase)) {
                isMatch = false;
                break;
            }
        }
        return isMatch;
    }
}
