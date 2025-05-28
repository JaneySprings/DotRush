
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
}