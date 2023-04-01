using LanguageServer.Parameters;
using LanguageServer.Parameters.TextDocument;
using CodeAnalysis = Microsoft.CodeAnalysis;

namespace dotRush.Server.Extensions;

public static class SymbolConverter {
    public static CompletionItemKind ToCompletionKind(this string tag) {
        switch (tag) {
            case "Class": return CompletionItemKind.Class;
            case "Delegate": return CompletionItemKind.Function;
            case "Enum": return CompletionItemKind.Enum;
            case "EnumMember": return CompletionItemKind.EnumMember;
            case "Interface": return CompletionItemKind.Interface;
            case "Struct": return CompletionItemKind.Struct;
            case "Local": return CompletionItemKind.Variable;
            case "Parameter": return CompletionItemKind.Variable;
            case "RangeVariable": return CompletionItemKind.Variable;
            case "Const": return CompletionItemKind.Constant;
            case "Event": return CompletionItemKind.Event;
            case "Field": return CompletionItemKind.Field;
            case "Method": return CompletionItemKind.Method;
            case "Property": return CompletionItemKind.Property;
            case "Label": return CompletionItemKind.Unit;
            case "Keyword": return CompletionItemKind.Keyword;
            case "Namespace": return CompletionItemKind.Module;
            case "ExtensionMethod": return CompletionItemKind.Method;
        }

        return CompletionItemKind.Text;
    }

    public static SymbolKind ToSymbolKind(this CodeAnalysis.SymbolKind kind) {
        switch (kind) {
            case CodeAnalysis.SymbolKind.ArrayType: return SymbolKind.Array;
            case CodeAnalysis.SymbolKind.Assembly: return SymbolKind.Module;
            case CodeAnalysis.SymbolKind.DynamicType: return SymbolKind.Class;
            case CodeAnalysis.SymbolKind.ErrorType: return SymbolKind.Class;
            case CodeAnalysis.SymbolKind.Event: return SymbolKind.Event;
            case CodeAnalysis.SymbolKind.Field: return SymbolKind.Field;
            case CodeAnalysis.SymbolKind.Label: return SymbolKind.Variable;
            case CodeAnalysis.SymbolKind.Local: return SymbolKind.Variable;
            case CodeAnalysis.SymbolKind.Method: return SymbolKind.Method;
            case CodeAnalysis.SymbolKind.NetModule: return SymbolKind.Module;
            case CodeAnalysis.SymbolKind.NamedType: return SymbolKind.Class;
            case CodeAnalysis.SymbolKind.Namespace: return SymbolKind.Module;
            case CodeAnalysis.SymbolKind.Parameter: return SymbolKind.Variable;
            case CodeAnalysis.SymbolKind.PointerType: return SymbolKind.Class;
            case CodeAnalysis.SymbolKind.Property: return SymbolKind.Property;
            case CodeAnalysis.SymbolKind.RangeVariable: return SymbolKind.Variable;
            case CodeAnalysis.SymbolKind.TypeParameter: return SymbolKind.Class;
        }

        return SymbolKind.File;
    }
}