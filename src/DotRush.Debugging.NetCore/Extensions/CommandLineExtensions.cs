using System.CommandLine;
using DotRush.Common.Extensions;

namespace DotRush.Debugging.NetCore.Extensions;

internal static class CommandLineExtensions {
    public static string? GetTrimmedValue(this ParseResult result, Option<string> option) {
        var rawValue = result.GetValue(option);
        if (!string.IsNullOrEmpty(rawValue))
            return TrimPath(rawValue);
        return rawValue;
    }
    public static string[] GetTrimmedValue(this ParseResult result, Option<string[]> option) {
        var rawValues = result.GetValue(option);
        if (rawValues != null && rawValues.Length > 0)
            return rawValues.Select(TrimPath).ToArray();
        return rawValues ?? Array.Empty<string>();
    }

    private static string TrimPath(string rawPath) {
        return rawPath.Trim('"', '\'').ToPlatformPath();
    }
}