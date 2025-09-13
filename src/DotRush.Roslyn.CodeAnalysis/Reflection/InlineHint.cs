using System.Collections.Immutable;
using System.Reflection;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public static class InternalInlineHint {
    internal static readonly Type? inlineHintType;
    internal static readonly FieldInfo? spanField;
    internal static readonly FieldInfo? replacementTextChangeField;
    internal static readonly FieldInfo? displatPartsField;

    static InternalInlineHint() {
        inlineHintType = ReflectionExtensions.GetTypeFromLoadedAssembly(KnownAssemblies.CommonFeaturesAssemblyName, "Microsoft.CodeAnalysis.InlineHints.InlineHint");
        spanField = inlineHintType?.GetField("Span");
        replacementTextChangeField = inlineHintType?.GetField("ReplacementTextChange");
        displatPartsField = inlineHintType?.GetField("DisplayParts");
    }

    public static TextSpan GetSpan(object? inlineHint) {
        var result = spanField?.GetValue(inlineHint);
        if (result is TextSpan textSpan)
            return textSpan;

        return default(TextSpan);
    }
    public static TextChange? GetReplacementTextChange(object? inlineHint) {
        var result = replacementTextChangeField?.GetValue(inlineHint);
        if (result is TextChange textChange)
            return textChange;

        return null;
    }
    public static ImmutableArray<TaggedText> GetDisplayParts(object? inlineHint) {
        var result = displatPartsField?.GetValue(inlineHint);
        if (result is ImmutableArray<TaggedText> taggedTexts)
            return taggedTexts;

        return ImmutableArray<TaggedText>.Empty;
    }
}