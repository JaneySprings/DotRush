
namespace DotRush.Common.Extensions;

public static class StringExtensions {
    public static bool StartsWithUpper(this string value) {
        if (string.IsNullOrEmpty(value))
            return false;

        return char.IsUpper(value[0]);
    }
    public static string ToCamelCase(this string value) {
        if (string.IsNullOrEmpty(value))
            return value;

        if (value.Length == 1)
            return value.ToLowerInvariant();

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    public static string[] SplitByCamelCase(this string value) {
        var parts = new List<string>();
        if (string.IsNullOrEmpty(value))
            return Array.Empty<string>();

        int wordStartIndex = 0;
        for (int i = 1; i < value.Length; i++) {
            if (char.IsUpper(value[i]) && !char.IsUpper(value[i - 1])) {
                parts.Add(value.Substring(wordStartIndex, i - wordStartIndex));
                wordStartIndex = i;
            }
        }

        parts.Add(value.Substring(wordStartIndex));
        return parts.ToArray();
    }
}