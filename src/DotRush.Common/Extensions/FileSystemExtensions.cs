namespace DotRush.Common.Extensions;

public static class FileSystemExtensions {
    public static void WriteAllText(string filePath, string content) {
        if (!TryDeleteFile(filePath))
            return;

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
            return true;

        try {
            Directory.Delete(directoryPath, true);
            return true;
        } catch {
            return false;
        }
    }
    public static bool TryDeleteFile(string filePath) {
        if (!File.Exists(filePath))
            return true;

        try {
            File.Delete(filePath);
            return true;
        } catch {
            return false;
        }
    }
    public static void MakeFileReadOnly(string filePath) {
        if (!File.Exists(filePath))
            return;

        File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly);
    }
    public static string RenameFile(string sourceFilePath, string newName) {
        var sourceFileDirectory = Path.GetDirectoryName(sourceFilePath);
        var newFilePath = Path.Combine(sourceFileDirectory!, newName);

        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException($"Source file not found: {sourceFilePath}");
        if (File.Exists(newFilePath))
            throw new IOException($"Destination file already exists: {newFilePath}");

        File.Move(sourceFilePath, newFilePath);
        return newFilePath;
    }
    public static string[] GetFiles(string path, string[] extensions, SearchOption searchOption) {
        if (!Directory.Exists(path))
            return Array.Empty<string>();

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in extensions)
            result.UnionWith(Directory.EnumerateFiles(path, $"*.{extension}", searchOption));

        return result.ToArray();
    }
}
