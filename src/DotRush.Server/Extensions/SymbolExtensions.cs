using Microsoft.CodeAnalysis;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

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

    public static ProtocolModels.SymbolKind ToSymbolKind(this ISymbol symbol) {
        if (symbol is IAliasSymbol aliasSymbol)
            return aliasSymbol.Target.ToSymbolKind();

        if (symbol is IFieldSymbol fieldSymbol) {
            if (fieldSymbol.ContainingType?.TypeKind == TypeKind.Enum)
                return ProtocolModels.SymbolKind.EnumMember;

            return fieldSymbol.IsConst ? ProtocolModels.SymbolKind.Constant : ProtocolModels.SymbolKind.Field;
        }

        if (symbol is INamedTypeSymbol namedType) {
            switch (namedType.TypeKind) {
                case TypeKind.Unknown: return ProtocolModels.SymbolKind.Null;
                case TypeKind.Array: return ProtocolModels.SymbolKind.Array;
                case TypeKind.Class: return ProtocolModels.SymbolKind.Class;
                case TypeKind.Delegate: return ProtocolModels.SymbolKind.Function;
                case TypeKind.Dynamic: return ProtocolModels.SymbolKind.Object;
                case TypeKind.Enum: return ProtocolModels.SymbolKind.Enum;
                case TypeKind.Error: return ProtocolModels.SymbolKind.String;
                case TypeKind.Interface: return ProtocolModels.SymbolKind.Interface;
                case TypeKind.Module: return ProtocolModels.SymbolKind.Module;
                case TypeKind.Pointer: return ProtocolModels.SymbolKind.Object;
                case TypeKind.Struct: return ProtocolModels.SymbolKind.Struct;
                case TypeKind.TypeParameter: return ProtocolModels.SymbolKind.TypeParameter;
                case TypeKind.Submission: return ProtocolModels.SymbolKind.Object;
                case TypeKind.FunctionPointer: return ProtocolModels.SymbolKind.Function;
            }
        }

        switch (symbol.Kind) {
            case SymbolKind.ArrayType: return ProtocolModels.SymbolKind.Array;
            case SymbolKind.Assembly: return ProtocolModels.SymbolKind.Module;
            case SymbolKind.DynamicType: return ProtocolModels.SymbolKind.Object;
            case SymbolKind.ErrorType: return ProtocolModels.SymbolKind.Null;
            case SymbolKind.Event: return ProtocolModels.SymbolKind.Event;
            case SymbolKind.Field: return ProtocolModels.SymbolKind.Field;
            case SymbolKind.Label: return ProtocolModels.SymbolKind.String;
            case SymbolKind.Local: return ProtocolModels.SymbolKind.Variable;
            case SymbolKind.Method: return ProtocolModels.SymbolKind.Method;
            case SymbolKind.NetModule: return ProtocolModels.SymbolKind.Module;
            case SymbolKind.Namespace: return ProtocolModels.SymbolKind.Namespace;
            case SymbolKind.Parameter: return ProtocolModels.SymbolKind.Variable;
            case SymbolKind.PointerType: return ProtocolModels.SymbolKind.Object;
            case SymbolKind.Property: return ProtocolModels.SymbolKind.Property;
            case SymbolKind.RangeVariable: return ProtocolModels.SymbolKind.Variable;
            case SymbolKind.TypeParameter: return ProtocolModels.SymbolKind.TypeParameter;
            case SymbolKind.Preprocessing: return ProtocolModels.SymbolKind.Constant;
            case SymbolKind.Discard: return ProtocolModels.SymbolKind.Variable;
            case SymbolKind.FunctionPointerType: return ProtocolModels.SymbolKind.Function;
        }

        return ProtocolModels.SymbolKind.String;
    }
}