using Microsoft.CodeAnalysis;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Message.DocumentSymbol;

namespace DotRush.Roslyn.Server.Extensions;

public static class SymbolExtensions {
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
    public static SemanticTokenType ToSemanticTokenType(this ISymbol symbol) {
        switch (symbol.ToSymbolKind()) {
            case ProtocolModels.SymbolKind.Class:
                return SemanticTokenType.Class;
            case ProtocolModels.SymbolKind.Struct:
                return SemanticTokenType.Struct;
            case ProtocolModels.SymbolKind.Namespace:
                return SemanticTokenType.Namespace;
            case ProtocolModels.SymbolKind.Enum:
                return SemanticTokenType.Enum;
            case ProtocolModels.SymbolKind.EnumMember:
                return SemanticTokenType.EnumMember;
            case ProtocolModels.SymbolKind.Interface:
                return SemanticTokenType.Interface;
            case ProtocolModels.SymbolKind.TypeParameter:
                return SemanticTokenType.TypeParameter;
            case ProtocolModels.SymbolKind.Method:
                if (symbol is IMethodSymbol methodSymbol && methodSymbol.MethodKind == MethodKind.Constructor)
                    return methodSymbol.ContainingType.ToSemanticTokenType();
                return SemanticTokenType.Method;
            case ProtocolModels.SymbolKind.Property:
                return SemanticTokenType.Property;
            case ProtocolModels.SymbolKind.Event:
                return SemanticTokenType.Event;
            case ProtocolModels.SymbolKind.Constant:
                return SemanticTokenType.Variable;
            case ProtocolModels.SymbolKind.Field:
                return SemanticTokenType.Variable;
            case ProtocolModels.SymbolKind.Variable:
                if (symbol is IParameterSymbol)
                    return SemanticTokenType.Parameter;
                return SemanticTokenType.Variable;
            case ProtocolModels.SymbolKind.String:
                if (symbol.Kind == SymbolKind.Label)
                    return SemanticTokenType.Label;
                return SemanticTokenType.String;
            case ProtocolModels.SymbolKind.Operator:
                return SemanticTokenType.Operator;
            case ProtocolModels.SymbolKind.Function:
                return SemanticTokenType.Method;
        }

        return SemanticTokenType.Unknown;
    }
}

public enum SemanticTokenType {
    Comment,
    Keyword,
    ControlKeyword,
    Number,
    Operator,
    String,
    Class,
    Struct,
    Namespace,
    Enum,
    Interface,
    TypeParameter,
    Parameter,
    Variable,
    Property,
    Method,
    EnumMember,
    Event,
    Macro,
    Label,
    Unknown
}