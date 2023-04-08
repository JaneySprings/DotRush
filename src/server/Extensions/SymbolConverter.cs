using LanguageServer.Parameters.TextDocument;

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

    public static CompletionItem ToCompletionItem(this Microsoft.CodeAnalysis.Completion.CompletionItem item) {
        return new LanguageServer.Parameters.TextDocument.CompletionItem() {
            label = item.DisplayText,
            sortText = item.SortText,
            filterText = item.FilterText,
            kind = item.Tags.First().ToCompletionKind(),
            insertTextFormat = InsertTextFormat.PlainText,
            preselect = item.Rules.MatchPriority == Microsoft.CodeAnalysis.Completion.MatchPriority.Preselect
        };
    }
}