namespace DotRush.Essentials.Common.Extensions;

public static class PathExtensions {
    public static string ToPlatformPath(this string path) {
        return path
            .Replace('\\', System.IO.Path.DirectorySeparatorChar)
            .Replace('/', System.IO.Path.DirectorySeparatorChar)
            .Replace("\\\\", $"{System.IO.Path.DirectorySeparatorChar}")
            .Replace("//", $"{System.IO.Path.DirectorySeparatorChar}");
    }
    public static string TrimPathEnd(this string path) {
        return path.TrimEnd(System.IO.Path.DirectorySeparatorChar);
    }
}