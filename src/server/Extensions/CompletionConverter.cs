using Microsoft.CodeAnalysis.Text;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Microsoft.CodeAnalysis.Tags;

namespace DotRush.Server.Extensions;

public static class CompletionConverter {
    public static ProtocolModels.CompletionItemKind ToCompletionItemKind(this string tag) {
        switch (tag) {
            case WellKnownTags.Class: return ProtocolModels.CompletionItemKind.Class;
            case WellKnownTags.Delegate: return ProtocolModels.CompletionItemKind.Function;
            case WellKnownTags.Enum: return ProtocolModels.CompletionItemKind.Enum;
            case WellKnownTags.EnumMember: return ProtocolModels.CompletionItemKind.EnumMember;
            case WellKnownTags.Interface: return ProtocolModels.CompletionItemKind.Interface;
            case WellKnownTags.Structure: return ProtocolModels.CompletionItemKind.Struct;
            case WellKnownTags.Local:
            case WellKnownTags.Parameter:
            case WellKnownTags.RangeVariable: return ProtocolModels.CompletionItemKind.Variable;
            case WellKnownTags.Constant: return ProtocolModels.CompletionItemKind.Constant;
            case WellKnownTags.Event: return ProtocolModels.CompletionItemKind.Event;
            case WellKnownTags.Field: return ProtocolModels.CompletionItemKind.Field;
            case WellKnownTags.Method: return ProtocolModels.CompletionItemKind.Method;
            case WellKnownTags.Property: return ProtocolModels.CompletionItemKind.Property;
            case WellKnownTags.Label: return ProtocolModels.CompletionItemKind.Text;
            case WellKnownTags.Keyword: return ProtocolModels.CompletionItemKind.Keyword;
            case WellKnownTags.Namespace: return ProtocolModels.CompletionItemKind.Module;
            case WellKnownTags.ExtensionMethod: return ProtocolModels.CompletionItemKind.Method;
            case WellKnownTags.Snippet: return ProtocolModels.CompletionItemKind.Snippet;
            case WellKnownTags.File: return ProtocolModels.CompletionItemKind.File;
            case WellKnownTags.Folder: return ProtocolModels.CompletionItemKind.Folder;
            case WellKnownTags.Project: return ProtocolModels.CompletionItemKind.Reference;
        }

        return ProtocolModels.CompletionItemKind.Text;
    }

    public static ProtocolModels.TextEdit ToTextEdit(this TextChange change, SourceText sourceText) {
        return new ProtocolModels.TextEdit() {
            NewText = change.NewText ?? string.Empty,
            Range = change.Span.ToRange(sourceText)
        };
    }
}