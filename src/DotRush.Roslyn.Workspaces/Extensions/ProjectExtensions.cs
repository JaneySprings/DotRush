using DotRush.Common.Extensions;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Workspaces.Extensions;

public static class ProjectExtensions {
    public static string GetTargetFramework(this Project project) {
        var frameworkStartIndex = project.Name.LastIndexOf('(');
        if (frameworkStartIndex == -1)
            return project.Name;

        return project.Name.Substring(frameworkStartIndex + 1, project.Name.Length - frameworkStartIndex - 2);
    }
    public static string GetOutputPath(this Project project) {
        var fallbackPath = Path.Combine(project.GetProjectDirectory(), "bin");
        if (string.IsNullOrEmpty(project.OutputFilePath))
            return fallbackPath;

        var directory = new DirectoryInfo(Path.GetDirectoryName(project.OutputFilePath)!);
        if (directory.Name.Contains("net", StringComparison.OrdinalIgnoreCase))
            return directory.Parent?.FullName ?? fallbackPath;

        return directory.FullName;
    }
    public static string GetIntermediateOutputPath(this Project project) {
        var fallbackPath = Path.Combine(project.GetProjectDirectory(), "obj");
        if (string.IsNullOrEmpty(project.OutputRefFilePath))
            return fallbackPath;

        var directory = new DirectoryInfo(Path.GetDirectoryName(project.OutputRefFilePath)!);
        if (directory.Name.Equals("ref", StringComparison.OrdinalIgnoreCase))
            return directory.Parent?.FullName ?? fallbackPath;

        return directory.FullName;
    }
    public static string GetProjectDirectory(this Project project) {
        return Path.GetDirectoryName(project.FilePath) ?? string.Empty;
    }
    public static string GetDocumentFilePath(this Document document) {
        if (!string.IsNullOrEmpty(document.FilePath))
            return document.FilePath;

        var documentFilePath = document.Project.GetProjectDirectory();
        document.Folders.ForEach(folder => documentFilePath = Path.Combine(documentFilePath, folder));
        return Path.Combine(documentFilePath, document.Name);
    }

    public static IEnumerable<DocumentId> GetDocumentIdsWithFilePath(this Project project, string? filePath) {
        return project.Documents.Where(it => PathExtensions.Equals(it.FilePath, filePath)).Select(it => it.Id);
    }
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFilePath(this Project project, string? filePath) {
        return project.AdditionalDocuments.Where(it => PathExtensions.Equals(it.FilePath, filePath)).Select(it => it.Id);
    }
    public static IEnumerable<Document> GetDocumentsWithDirectoryPath(this Project project, string? dirPath) {
        return project.Documents.Where(it => PathExtensions.StartsWith(it.FilePath, dirPath));
    }
    public static IEnumerable<TextDocument> GetAdditionalDocumentsWithDirectoryPath(this Project project, string? dirPath) {
        return project.AdditionalDocuments.Where(it => PathExtensions.StartsWith(it.FilePath, dirPath));
    }

    /// <summary>Folders of a document relative to the project directory, empty for documents outside of it.</summary>
    public static IEnumerable<string> GetFolders(string? projectDirectory, string documentPath) {
        var documentDirectory = Path.GetDirectoryName(documentPath);
        if (string.IsNullOrEmpty(documentDirectory) || string.IsNullOrEmpty(projectDirectory))
            return Enumerable.Empty<string>();

        var relativePath = Path.GetRelativePath(projectDirectory, documentDirectory);
        if (relativePath == "." || relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
            return Enumerable.Empty<string>();

        return relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Where(it => !string.IsNullOrEmpty(it));
    }
}
