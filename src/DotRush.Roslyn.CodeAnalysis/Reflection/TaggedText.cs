using System.Reflection;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public static class InternalTaggedText {
    internal static readonly PropertyInfo? styleProperty;
    internal static readonly PropertyInfo? navigationTargetProperty;
    internal static readonly PropertyInfo? navigationHintProperty;

    public const int StyleNone = 0x0;
    public const int StyleStrong = 0x1;
    public const int StyleEmphasis = 0x2;
    public const int StyleUnderline = 0x4;
    public const int StyleCode = 0x8;
    public const int StylePreserveWhitespace = 0x10;

    static InternalTaggedText() {
        var taggedTextType = typeof(TaggedText);
        styleProperty = taggedTextType.GetProperty("Style", BindingFlags.NonPublic | BindingFlags.Instance);
        navigationTargetProperty = taggedTextType.GetProperty("NavigationTarget", BindingFlags.NonPublic | BindingFlags.Instance);
        navigationHintProperty = taggedTextType.GetProperty("NavigationHint", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    public static int GetStyle(TaggedText taggedText) {
        if (styleProperty == null)
            return StyleNone;

        var result = styleProperty.GetValue(taggedText);
        if (result == null)
            return StyleNone;

        return Convert.ToInt32(result);
    }
    public static string? GetNavigationTarget(TaggedText taggedText) {
        if (navigationTargetProperty == null)
            return null;

        return navigationTargetProperty.GetValue(taggedText) as string;
    }
    public static string? GetNavigationHint(TaggedText taggedText) {
        if (navigationHintProperty == null)
            return null;

        return navigationHintProperty.GetValue(taggedText) as string;
    }
}