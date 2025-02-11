using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tags;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Extensions;

public static class CompletionExtensions {
    public static ProtocolModels.CompletionItemKind ToCompletionItemKind(this string tag) {
        switch (tag) {
            case WellKnownTags.Public: 
            case WellKnownTags.Protected:
            case WellKnownTags.Private:
            case WellKnownTags.Internal: return ProtocolModels.CompletionItemKind.Keyword;
            case WellKnownTags.File:
            case WellKnownTags.Project:
            case WellKnownTags.Folder: return ProtocolModels.CompletionItemKind.File;
            case WellKnownTags.Assembly: return ProtocolModels.CompletionItemKind.Module;

            case WellKnownTags.Class: return ProtocolModels.CompletionItemKind.Class;
            case WellKnownTags.Constant: return ProtocolModels.CompletionItemKind.Constant;
            case WellKnownTags.Delegate: return ProtocolModels.CompletionItemKind.Interface;
            
            case WellKnownTags.Enum: return ProtocolModels.CompletionItemKind.Enum;
            case WellKnownTags.EnumMember: return ProtocolModels.CompletionItemKind.EnumMember;
            case WellKnownTags.Event: return ProtocolModels.CompletionItemKind.Event;
            case WellKnownTags.ExtensionMethod: return ProtocolModels.CompletionItemKind.Method;
            case WellKnownTags.Field: return ProtocolModels.CompletionItemKind.Field;
            case WellKnownTags.Interface: return ProtocolModels.CompletionItemKind.Interface;
            case WellKnownTags.Intrinsic: return ProtocolModels.CompletionItemKind.Module;
            case WellKnownTags.Keyword: return ProtocolModels.CompletionItemKind.Keyword;
            case WellKnownTags.Label: return ProtocolModels.CompletionItemKind.Text;
            case WellKnownTags.Local: return ProtocolModels.CompletionItemKind.Variable;
            case WellKnownTags.Namespace: return ProtocolModels.CompletionItemKind.Module;
            case WellKnownTags.Method: return ProtocolModels.CompletionItemKind.Method;
            case WellKnownTags.Module: return ProtocolModels.CompletionItemKind.Module;
            case WellKnownTags.Operator: return ProtocolModels.CompletionItemKind.Operator;
            case WellKnownTags.Parameter: return ProtocolModels.CompletionItemKind.Variable;
            case WellKnownTags.Property: return ProtocolModels.CompletionItemKind.Property;
            case WellKnownTags.RangeVariable: return ProtocolModels.CompletionItemKind.Variable;
            case WellKnownTags.Reference: return ProtocolModels.CompletionItemKind.Reference;
            case WellKnownTags.Structure: return ProtocolModels.CompletionItemKind.Struct;
            case WellKnownTags.TypeParameter: return ProtocolModels.CompletionItemKind.TypeParameter;
            case WellKnownTags.Snippet: return ProtocolModels.CompletionItemKind.Snippet;
            case WellKnownTags.Error:
            case WellKnownTags.Warning: return ProtocolModels.CompletionItemKind.Text;
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