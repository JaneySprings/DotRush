using DotRush.Roslyn.CodeAnalysis.Reflection;
using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.CodeAnalysis.Text;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Message.Completion;

namespace DotRush.Roslyn.Server.Extensions;

public static class CompletionExtensions {
    public static ProtocolModels.CompletionItemKind ToCompletionItemKind(this CompletionItem item) {
        if (item.Tags.Length == 0)
            return ProtocolModels.CompletionItemKind.Text;

        switch (item.Tags[0]) {
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

    public static bool HasPriority(this CompletionItem item) {
        if (item.Rules.MatchPriority == Microsoft.CodeAnalysis.Completion.MatchPriority.Preselect)
            return true;
        if (item.Tags.Contains(InternalWellKnownTags.TargetTypeMatch))
            return true;

        return false;
    }
    public static bool IsAutoUsing(this CompletionItem item) {
        //(flags & CompletionItemFlags.Expanded) != 0
        return (InternalCompletionItem.GetFlags(item) & InternalCompletionItem.FlagExpanded) != 0;
    }
    public static bool IsSnippet(this CompletionItem item) {
        return item.Tags.Contains(WellKnownTags.Snippet);
    }

    public static TextEdit ToTextEdit(this TextChange change, SourceText sourceText) {
        return new TextEdit() {
            NewText = change.NewText ?? string.Empty,
            Range = change.Span.ToRange(sourceText)
        };
    }
    public static AnnotatedTextEdit ToAnnotatedTextEdit(this TextChange change, SourceText sourceText) {
        return new AnnotatedTextEdit() {
            NewText = change.NewText ?? string.Empty,
            Range = change.Span.ToRange(sourceText)
        };
    }
    public static (TextEdit?, List<AnnotatedTextEdit>) ToTextChanges(this CompletionChange completionChange, SourceText sourceText, int cursorOffset) {
        var additionalTextEdits = completionChange.TextChanges.Where(x => !x.Span.IntersectsWith(cursorOffset)).Select(x => x.ToAnnotatedTextEdit(sourceText)).ToList();
        var currentLineTextChanges = completionChange.TextChanges.Where(x => x.Span.IntersectsWith(cursorOffset)).ToList();
        if (currentLineTextChanges.Count == 0)
            return (null, additionalTextEdits);

        var currentLineTextChange = currentLineTextChanges.First(); // Single?
        var snippetText = RemoveIndentation(InternalCompletionChange.GetProperty(completionChange, InternalCompletionChange.SnippetTextKey));
        var newText = (string.IsNullOrEmpty(snippetText) ? currentLineTextChange.NewText : snippetText) ?? string.Empty;
        var oldLineCount = additionalTextEdits.Sum(x => x.Range.Height());
        var newLineCount = additionalTextEdits.SelectMany(x => x.NewText.Split('\n')).Count();

        var textEdit = new TextEdit() {
            NewText = newText,
            Range = currentLineTextChange.Span.ToRange(sourceText).Offset(newLineCount - oldLineCount)
        };

        return (textEdit, additionalTextEdits);
    }

    public static Task<CompletionList> GetCompletionsAsync(this CompletionService completionService, Document document, int position, ConfigurationService configurationService, CancellationToken cancellationToken) {
        var completionOptions = InternalCompletionOptions.CreateNew();
        if (completionOptions != null)
            InternalCompletionOptions.AssignValues(completionOptions,
                configurationService.ShowItemsFromUnimportedNamespaces,
                configurationService.TargetTypedCompletionFilter,
                forceExpandedCompletionIndexCreation: true);

        if (!InternalCompletionService.IsInitialized || completionOptions == null)
            return completionService.GetCompletionsAsync(document, position, cancellationToken: cancellationToken);

        return InternalCompletionService.GetCompletionsAsync(completionService, document, position, completionOptions, cancellationToken);
    }

    private static string? RemoveIndentation(string? text) {
        static int CountIndent(string text) {
            int result = 0;
            foreach (var symbol in text) {
                if (symbol != ' ' && symbol != '\t')
                    break;
                result++;
            }
            return result;
        }

        if (string.IsNullOrEmpty(text))
            return text;

        var eol = text.Contains("\r\n") ? "\r\n" : "\n";
        var lines = text.Split(eol, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            return text;

        var minIndent = int.MaxValue;
        for (int i = 1; i < lines.Length; i++)
            minIndent = Math.Min(minIndent, CountIndent(lines[i]));

        if (minIndent == int.MaxValue || minIndent == 0)
            return text;

        return string.Join(eol, lines.Select(x => x.Substring(Math.Min(minIndent, CountIndent(x)))));
    }
}

class TextEditEqualityComparer : IEqualityComparer<TextEdit> {
    public static TextEditEqualityComparer Default { get; } = new TextEditEqualityComparer();

    public bool Equals(TextEdit? x, TextEdit? y) {
        if (x == null && y == null)
            return true;
        if (x == null || y == null)
            return false;

        return GetHashCode(x) == GetHashCode(y);
    }
    public int GetHashCode(TextEdit obj) {
        return HashCode.Combine(obj.NewText, obj.Range);
    }
}
