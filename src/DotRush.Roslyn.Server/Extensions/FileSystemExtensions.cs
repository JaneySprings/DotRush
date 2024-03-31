
using Microsoft.CodeAnalysis;

namespace DotRush.Server.Extensions;

public static class FileSystemExtensions {
    public static IEnumerable<string> GetVisibleDirectories(string directoryPath) {
        var directoryInfo = new DirectoryInfo(directoryPath);
        return directoryInfo.EnumerateDirectories().Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden)).Select(d => d.FullName);
    }
    public static IEnumerable<string> GetVisibleFiles(string directoryPath) {
        var directoryInfo = new DirectoryInfo(directoryPath);
        return directoryInfo.EnumerateFiles().Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden)).Select(d => d.FullName);
    }
    public static IEnumerable<string> GetVisibleFilesRecursive(string directoryPath) {
        var directoryInfo = new DirectoryInfo(directoryPath);
        if (directoryInfo.Attributes.HasFlag(FileAttributes.Hidden))
            return Enumerable.Empty<string>();

        var result = new List<string>();
        result.AddRange(GetVisibleFiles(directoryPath));
        foreach (var directory in GetVisibleDirectories(directoryPath))
            result.AddRange(GetVisibleFilesRecursive(directory));

        return result;
    }
    public static bool IsFileVisible(string filePath, Project project) {
        var directory = Path.GetDirectoryName(project.FilePath);
        var folders = project.GetFolders(filePath);
        if (string.IsNullOrEmpty(directory))
            return false;

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
            return false;

        foreach (var folder in folders) {
            directory = Path.Combine(directory, folder);
            var directoryInfo = new DirectoryInfo(directory);
            if (directoryInfo.Attributes.HasFlag(FileAttributes.Hidden))
                return false;
        }

        return true;
    }

    public static bool PathEquals(string? path1, string? path2) {
        if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2))
            return false;

        path1 = Path.GetFullPath(path1);
        path2 = Path.GetFullPath(path2);
        return string.Equals(path1, path2, StringComparison.OrdinalIgnoreCase);
    }
}