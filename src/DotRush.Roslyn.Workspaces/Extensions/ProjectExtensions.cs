using Microsoft.CodeAnalysis;
using FileSystemExtensions = DotRush.Roslyn.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.Workspaces.Extensions;

public static class ProjectExtensions {

    public static string GetTargetFramework(this Project project) {
        var frameworkStartIndex = project.Name.LastIndexOf('(');
        if (frameworkStartIndex == -1)
            return string.Empty;

        return project.Name.Substring(frameworkStartIndex + 1, project.Name.Length - frameworkStartIndex - 2);
    }
    public static string GetOutputPath(this Project project) {
        return FirstFolderOrDefault(project.FilePath, project.OutputFilePath, $"bin{Path.DirectorySeparatorChar}");
    }
    public static string GetIntermediateOutputPath(this Project project) {
        return FirstFolderOrDefault(project.FilePath, project.OutputRefFilePath, $"obj{Path.DirectorySeparatorChar}");
    }

    public static IEnumerable<DocumentId> GetDocumentIdsWithFolderPath(this Project project, string folderPath) {
        var folderPathFixed = folderPath.EndsWith(Path.DirectorySeparatorChar) ? folderPath : folderPath + Path.DirectorySeparatorChar;
        return project.Documents
            .Where(document => document.FilePath?.StartsWith(folderPathFixed, StringComparison.OrdinalIgnoreCase) == true)
            .Select(document => document.Id);
    }
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFolderPath(this Project project, string folderPath) {
        var folderPathFixed = folderPath.EndsWith(Path.DirectorySeparatorChar) ? folderPath : folderPath + Path.DirectorySeparatorChar;
        return project.AdditionalDocuments
            .Where(document => document.FilePath?.StartsWith(folderPathFixed, StringComparison.OrdinalIgnoreCase) == true)
            .Select(document => document.Id);
    }
    public static IEnumerable<DocumentId> GetDocumentIdsWithFilePath(this Project project, string filePath) {
        return project.Documents.Where(it => FileSystemExtensions.PathEquals(it.FilePath, filePath)).Select(it => it.Id);
    }
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFilePath(this Project project, string filePath) {
        return project.AdditionalDocuments.Where(it => FileSystemExtensions.PathEquals(it.FilePath, filePath)).Select(it => it.Id);
    }

    public static IEnumerable<string> GetFolders(this Project project, string documentPath) {
        var rootDirectory = FileSystemExtensions.NormalizePath(Path.GetDirectoryName(project.FilePath) ?? string.Empty);
        var documentDirectory = FileSystemExtensions.NormalizePath(Path.GetDirectoryName(documentPath) ?? string.Empty);
        if (string.IsNullOrEmpty(documentDirectory) || string.IsNullOrEmpty(rootDirectory))
            return Enumerable.Empty<string>();

        var relativePath = documentDirectory.Replace(rootDirectory, string.Empty, StringComparison.OrdinalIgnoreCase);
        return relativePath.Split(Path.DirectorySeparatorChar).Where(it => !string.IsNullOrEmpty(it));
    }

    internal static string FirstFolderOrDefault(string? projectPath, string? targetPath, string fallbackFolder) {
        var projectDirectory = Path.GetDirectoryName(projectPath) + Path.DirectorySeparatorChar;
        if (targetPath == null || !targetPath.StartsWith(projectDirectory))
            return Path.Combine(projectDirectory, fallbackFolder);

        var relativePath = targetPath.Replace(projectDirectory, string.Empty);
        if (string.IsNullOrEmpty(relativePath))
            return Path.Combine(projectDirectory, fallbackFolder);

        return projectDirectory + relativePath.Split(Path.DirectorySeparatorChar).First() + Path.DirectorySeparatorChar;
    }
    internal static bool HasFolder(this Project project, string folderName) {
        return project.Documents.Any(it => it.Folders.Contains(folderName, StringComparer.OrdinalIgnoreCase));
    }
}
