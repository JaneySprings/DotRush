
namespace DotRush.Server.Extensions;

public static class PathConverter {
    public static string ToSystemPath(this Uri uri) {
        if (!RuntimeSystem.IsWindows)
            return uri.LocalPath;
        // Ignore first '/' in Windows path
        return Path.GetFullPath(uri.LocalPath.Substring(1));
    }

    public static Uri ToUri(this string path) {
        if (!RuntimeSystem.IsWindows)
            return new Uri(path);
        // Strange format only for Windows
        return new Uri("file:///" + path.Replace('\\', '/'));
    }
}