using LanguageServer.Parameters;
using LanguageServer.Parameters.TextDocument;
using Microsoft.CodeAnalysis;

namespace dotRush.Server.Extensions;

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