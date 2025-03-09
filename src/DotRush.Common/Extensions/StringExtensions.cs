
namespace DotRush.Common.Extensions;

public static class StringExtensions {
    public static bool StartsWithUpper(this string value) {
        if (string.IsNullOrEmpty(value))
            return false;

        return char.IsUpper(value[0]);
    }
}