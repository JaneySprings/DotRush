using Microsoft.CodeAnalysis;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Extensions;

public static class CompletionConverter {
    public static ProtocolModels.CompletionItemKind ToCompletionKind(this string tag) {
        switch (tag) {
            case "Class": return ProtocolModels.CompletionItemKind.Class;
            case "Delegate": return ProtocolModels.CompletionItemKind.Function;
            case "Enum": return ProtocolModels.CompletionItemKind.Enum;
            case "EnumMember": return ProtocolModels.CompletionItemKind.EnumMember;
            case "Interface": return ProtocolModels.CompletionItemKind.Interface;
            case "Structure": return ProtocolModels.CompletionItemKind.Struct;
            case "Local": return ProtocolModels.CompletionItemKind.Variable;
            case "Parameter": return ProtocolModels.CompletionItemKind.Variable;
            case "RangeVariable": return ProtocolModels.CompletionItemKind.Variable;
            case "Constant": return ProtocolModels.CompletionItemKind.Constant;
            case "Event": return ProtocolModels.CompletionItemKind.Event;
            case "Field": return ProtocolModels.CompletionItemKind.Field;
            case "Method": return ProtocolModels.CompletionItemKind.Method;
            case "Property": return ProtocolModels.CompletionItemKind.Property;
            case "Label": return ProtocolModels.CompletionItemKind.Unit;
            case "Keyword": return ProtocolModels.CompletionItemKind.Keyword;
            case "Namespace": return ProtocolModels.CompletionItemKind.Module;
            case "ExtensionMethod": return ProtocolModels.CompletionItemKind.Method;
        }

        return ProtocolModels.CompletionItemKind.Text;
    }

    public static ProtocolModels.CompletionItem ToCompletionItem(this Microsoft.CodeAnalysis.Completion.CompletionItem item) {
        return new ProtocolModels.CompletionItem() {
            Label = item.DisplayTextPrefix + item.DisplayText + item.DisplayTextSuffix,
            SortText = item.SortText,
            FilterText = item.FilterText,
            Detail = item.InlineDescription,
            Data = item.GetHashCode(),
            Kind = item.Tags.First().ToCompletionKind(),
            Preselect = item.Rules.MatchPriority == Microsoft.CodeAnalysis.Completion.MatchPriority.Preselect
        };
    }

    public static ProtocolModels.TextEdit ToTextEdit(this Microsoft.CodeAnalysis.Text.TextChange change, Document document) {
        return new ProtocolModels.TextEdit() {
            NewText = change.NewText ?? string.Empty,
            Range = change.Span.ToRange(document)
        };
    }

    public static ProtocolModels.TextEdit ToEmptyTextEdit(this Microsoft.CodeAnalysis.Text.TextChange change) {
        var empty = new ProtocolModels.Position(0, 0);
        return new ProtocolModels.TextEdit() {
            Range = new ProtocolModels.Range(empty, empty),
            NewText = string.Empty
        };
    }
}