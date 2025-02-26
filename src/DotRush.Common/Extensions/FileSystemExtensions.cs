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
}
