namespace DotRush.Common.Extensions;

public static class FileSystemExtensions {
    // public static IEnumerable<string> GetVisibleDirectories(string directoryPath) {
    //     var directoryInfo = new DirectoryInfo(directoryPath);
    //     return directoryInfo.EnumerateDirectories().Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden)).Select(d => d.FullName).ToArray();
    // }
    // public static IEnumerable<string> GetVisibleFiles(string directoryPath, Func<string, bool>? filter = null) {
    //     var directoryInfo = new DirectoryInfo(directoryPath);
    //     var files = directoryInfo.EnumerateFiles().Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden)).Select(d => d.FullName).ToArray();
    //     return filter == null ? files : files.Where(filter);
    // }
    // public static IEnumerable<string> GetVisibleFilesRecursive(string directoryPath) {
    //     var directoryInfo = new DirectoryInfo(directoryPath);
    //     if (directoryInfo.Attributes.HasFlag(FileAttributes.Hidden))
    //         return Enumerable.Empty<string>();

    //     var result = new List<string>();
    //     result.AddRange(GetVisibleFiles(directoryPath));
    //     foreach (var directory in GetVisibleDirectories(directoryPath))
    //         result.AddRange(GetVisibleFilesRecursive(directory));

    //     return result;
    // }
    // public static bool IsFileVisible(string? baseDirectoryPath, IEnumerable<string> folders, string fileName) {
    //     if (string.IsNullOrEmpty(baseDirectoryPath))
    //         return false;

    //     var filePath = folders.Any()
    //         ? Path.Combine(baseDirectoryPath, string.Join(Path.DirectorySeparatorChar, folders), fileName)
    //         : Path.Combine(baseDirectoryPath, fileName);
    //     var fileInfo = new FileInfo(filePath);
    //     if (fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
    //         return false;

    //     var directory = baseDirectoryPath;
    //     foreach (var folder in folders) {
    //         directory = Path.Combine(directory, folder);
    //         var directoryInfo = new DirectoryInfo(directory);
    //         if (directoryInfo.Attributes.HasFlag(FileAttributes.Hidden))
    //             return false;
    //     }

    //     return true;
    // }
    
    public static void WriteAllText(string filePath, string content) {
        if (File.Exists(filePath))
            File.Delete(filePath);

        var directoryPath = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath!);

        File.WriteAllText(filePath, content);
    }
    public static string TryReadText(string filePath) {
        if (!File.Exists(filePath))
            return string.Empty;

        try {
            return File.ReadAllText(filePath);
        } catch {
            return string.Empty;
        }
    }
    public static bool TryDeleteDirectory(string directoryPath) {
        if (!Directory.Exists(directoryPath))
            return false;

        try {
            Directory.Delete(directoryPath, true);
            return true;
        } catch {
            return false;
        }
    }
}
