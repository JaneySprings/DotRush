using LanguageServer.Parameters;
using LanguageServer.Parameters.TextDocument;
using Microsoft.CodeAnalysis;
using CASymbolKind = Microsoft.CodeAnalysis.SymbolKind;

namespace DotRush.Server.Extensions;

public static class SymbolConverter {
    public static CompletionItemKind ToCompletionKind(this string tag) {
        switch (tag) {
            case "Class": return CompletionItemKind.Class;
            case "Delegate": return CompletionItemKind.Function;
            case "Enum": return CompletionItemKind.Enum;
            case "EnumMember": return CompletionItemKind.EnumMember;
            case "Interface": return CompletionItemKind.Interface;
            case "Structure": return CompletionItemKind.Struct;
            case "Local": return CompletionItemKind.Variable;
            case "Parameter": return CompletionItemKind.Variable;
            case "RangeVariable": return CompletionItemKind.Variable;
            case "Constant": return CompletionItemKind.Constant;
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

    public static LanguageServer.Parameters.SymbolKind ToSymbolKind(this CASymbolKind kind) {
        switch (kind) {
            case CASymbolKind.ArrayType: return LanguageServer.Parameters.SymbolKind.Array;
            case CASymbolKind.Assembly: return LanguageServer.Parameters.SymbolKind.Module;
            case CASymbolKind.DynamicType: return LanguageServer.Parameters.SymbolKind.Variable;
            case CASymbolKind.ErrorType: return LanguageServer.Parameters.SymbolKind.Variable;
            case CASymbolKind.Event: return LanguageServer.Parameters.SymbolKind.Event;
            case CASymbolKind.Field: return LanguageServer.Parameters.SymbolKind.Field;
            case CASymbolKind.Label: return LanguageServer.Parameters.SymbolKind.Variable;
            case CASymbolKind.Local: return LanguageServer.Parameters.SymbolKind.Variable;
            case CASymbolKind.Method: return LanguageServer.Parameters.SymbolKind.Method;
            case CASymbolKind.NetModule: return LanguageServer.Parameters.SymbolKind.Module;
            case CASymbolKind.NamedType: return LanguageServer.Parameters.SymbolKind.Class;
            case CASymbolKind.Namespace: return LanguageServer.Parameters.SymbolKind.Module;
            case CASymbolKind.Parameter: return LanguageServer.Parameters.SymbolKind.Variable;
            case CASymbolKind.PointerType: return LanguageServer.Parameters.SymbolKind.Variable;
            case CASymbolKind.Property: return LanguageServer.Parameters.SymbolKind.Property;
            case CASymbolKind.RangeVariable: return LanguageServer.Parameters.SymbolKind.Variable;
            case CASymbolKind.TypeParameter: return LanguageServer.Parameters.SymbolKind.Variable;
        }

        return LanguageServer.Parameters.SymbolKind.Null;
    }

    public static string ToNamedString(this CASymbolKind kind) {
        switch (kind) {
            case CASymbolKind.ArrayType: return "Array";
            case CASymbolKind.DynamicType: return "dynamic";
            case CASymbolKind.ErrorType: return "Error";
            case CASymbolKind.Event: return "event";
            case CASymbolKind.NamedType: return "class";
            case CASymbolKind.Namespace: return "namespace";
        }

        return kind.ToString().ToLowerInvariant();
    }

    public static CompletionItem ToCompletionItem(this Microsoft.CodeAnalysis.Completion.CompletionItem item) {
        return new LanguageServer.Parameters.TextDocument.CompletionItem() {
            label = item.DisplayTextPrefix + item.DisplayText + item.DisplayTextSuffix,
            sortText = item.SortText,
            filterText = item.FilterText,
            detail = item.InlineDescription,
            data = item.GetHashCode(),
            kind = item.Tags.First().ToCompletionKind(),
            preselect = item.Rules.MatchPriority == Microsoft.CodeAnalysis.Completion.MatchPriority.Preselect
        };
    }

    public static TextEdit ToTextEdit(this Microsoft.CodeAnalysis.Text.TextChange change, Document document) {
        var start = change.Span.Start.ToPosition(document);
        var end = change.Span.End.ToPosition(document);

        return new TextEdit() {
            newText = change.NewText,
            range = new LanguageServer.Parameters.Range() {
                start = start,
                end = end
            }
        };
    }

    public static TextEdit ToEmptyTextEdit(this Microsoft.CodeAnalysis.Text.TextChange change) {
        var empty = new Position() { line = 0, character = 0 };
        return new TextEdit() {
            range = new LanguageServer.Parameters.Range() { start = empty, end = empty },
            newText = string.Empty
        };
    }
}