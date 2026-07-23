using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public static class InternalSymbolKey {
    internal static readonly Type? symbolKeyType;
    internal static readonly Type? symbolKeyResolutionType;
    internal static readonly MethodInfo? createStringMethod;
    internal static readonly MethodInfo? resolveStringMethod;
    internal static readonly PropertyInfo? resolutionSymbolProperty;
    internal static readonly PropertyInfo? resolutionCandidateSymbolsProperty;

    public static bool IsInitialized => createStringMethod != null && resolveStringMethod != null && resolutionSymbolProperty != null;

    static InternalSymbolKey() {
        symbolKeyType = typeof(SymbolFinder).Assembly.GetType("Microsoft.CodeAnalysis.SymbolKey");
        symbolKeyResolutionType = typeof(SymbolFinder).Assembly.GetType("Microsoft.CodeAnalysis.SymbolKeyResolution");
        createStringMethod = symbolKeyType?.GetMethod("CreateString", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, new[] { typeof(ISymbol), typeof(CancellationToken) });
        resolveStringMethod = symbolKeyType?.GetMethod("ResolveString", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, new[] { typeof(string), typeof(Compilation), typeof(bool), typeof(CancellationToken) });
        resolutionSymbolProperty = symbolKeyResolutionType?.GetProperty("Symbol", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        resolutionCandidateSymbolsProperty = symbolKeyResolutionType?.GetProperty("CandidateSymbols", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    public static string? CreateString(ISymbol symbol, CancellationToken cancellationToken) {
        if (createStringMethod == null)
            return null;

        return createStringMethod.Invoke(null, new object?[] { symbol, cancellationToken }) as string;
    }
    public static ISymbol? ResolveString(string symbolKey, Compilation compilation, CancellationToken cancellationToken) {
        if (resolveStringMethod == null || resolutionSymbolProperty == null)
            return null;

        var resolution = resolveStringMethod.Invoke(null, new object?[] { symbolKey, compilation, false, cancellationToken });
        if (resolution == null)
            return null;

        if (resolutionSymbolProperty.GetValue(resolution) is ISymbol symbol)
            return symbol;

        if (resolutionCandidateSymbolsProperty?.GetValue(resolution) is ImmutableArray<ISymbol> candidateSymbols && !candidateSymbols.IsDefaultOrEmpty)
            return candidateSymbols[0];

        return null;
    }
}
