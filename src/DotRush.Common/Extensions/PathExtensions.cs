namespace DotRush.Common.Extensions;

public static class PathExtensions {
    public static string ToPlatformPath(this string path) {
        return path
            .Replace('\\', System.IO.Path.DirectorySeparatorChar)
            .Replace('/', System.IO.Path.DirectorySeparatorChar)
            .Replace("\\\\", $"{System.IO.Path.DirectorySeparatorChar}")
            .Replace("//", $"{System.IO.Path.DirectorySeparatorChar}")
            .TrimPathStart();
    }
    public static string TrimPathEnd(this string path) {
        return path.TrimEnd('/', '\\');
    }
    public static string TrimPathStart(this string path) {
        if (RuntimeInfo.IsWindows)
            return path.TrimStart('/', '\\');
        // On Windows paths can start with a /c:/path 
        return path;
    }
    public static string GetFileSystemPath(this Uri uri) {
        return uri.LocalPath.TrimPathStart();
    }

    public static bool Equals(string? path1, string? path2) {
        if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2))
            return false;

        path1 = Path.GetFullPath(path1).ToPlatformPath();
        path2 = Path.GetFullPath(path2).ToPlatformPath();
        return string.Equals(path1, path2, StringComparison.OrdinalIgnoreCase);
    }
    public static bool StartsWith(string? path, string? value) {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(value))
            return false;

        path = Path.GetFullPath(path).ToPlatformPath();
        value = Path.GetFullPath(value).ToPlatformPath();
        return path.StartsWith(value, StringComparison.OrdinalIgnoreCase);
    }
}