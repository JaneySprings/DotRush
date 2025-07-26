using System.Globalization;
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

    public static string? GetInheritedDocumentationCommentXml(this ISymbol symbol, CultureInfo? preferredCulture = null, bool expandIncludes = false, CancellationToken cancellationToken = default(CancellationToken)) {
        var xml = symbol.GetDocumentationCommentXml(preferredCulture, expandIncludes, cancellationToken);
        if (!string.IsNullOrEmpty(xml))
            return xml;

        var overridenSymbol = symbol.GetOverridenSymbol();
        while (overridenSymbol != null) {
            xml = overridenSymbol.GetDocumentationCommentXml(preferredCulture, expandIncludes, cancellationToken);
            if (!string.IsNullOrEmpty(xml))
                return xml;

            overridenSymbol = overridenSymbol.GetOverridenSymbol();
        }

        var interfaceSymbol = symbol.GetImplementedInterfaceMember();
        if (interfaceSymbol != null) {
            xml = interfaceSymbol.GetDocumentationCommentXml(preferredCulture, expandIncludes, cancellationToken);
            if (!string.IsNullOrEmpty(xml))
                return xml;
        }

        return null;
    }
    public static ISymbol? GetOverridenSymbol(this ISymbol symbol) {
        if (symbol is IMethodSymbol methodSymbol)
            return methodSymbol.OverriddenMethod;
        if (symbol is IPropertySymbol propertySymbol)
            return propertySymbol.OverriddenProperty;
        if (symbol is IEventSymbol eventSymbol)
            return eventSymbol.OverriddenEvent;

        return null;
    }
    public static ISymbol? GetImplementedInterfaceMember(this ISymbol symbol) {
        if (symbol is IMethodSymbol methodSymbol) {
            foreach (var interfaceMethod in methodSymbol.ContainingType.AllInterfaces.SelectMany(i => i.GetMembers().OfType<IMethodSymbol>())) {
                var implementation = methodSymbol.ContainingType.FindImplementationForInterfaceMember(interfaceMethod);
                if (SymbolEqualityComparer.Default.Equals(implementation, methodSymbol))
                    return interfaceMethod;
            }
        }
        if (symbol is IPropertySymbol propertySymbol) {
            foreach (var interfaceProperty in propertySymbol.ContainingType.AllInterfaces.SelectMany(i => i.GetMembers().OfType<IPropertySymbol>())) {
                var implementation = propertySymbol.ContainingType.FindImplementationForInterfaceMember(interfaceProperty);
                if (SymbolEqualityComparer.Default.Equals(implementation, propertySymbol))
                    return interfaceProperty;
            }
        }
        if (symbol is IEventSymbol eventSymbol) {
            foreach (var interfaceEvent in eventSymbol.ContainingType.AllInterfaces.SelectMany(i => i.GetMembers().OfType<IEventSymbol>())) {
                var implementation = eventSymbol.ContainingType.FindImplementationForInterfaceMember(interfaceEvent);
                if (SymbolEqualityComparer.Default.Equals(implementation, eventSymbol))
                    return interfaceEvent;
            }
        }

        return null;
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
}
