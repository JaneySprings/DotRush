using DotRush.Roslyn.Common.Extensions;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Workspaces.Extensions;

public static class ProjectExtensions {

    public static string GetTargetFramework(this Project project) {
        var frameworkStartIndex = project.Name.LastIndexOf('(');
        if (frameworkStartIndex == -1)
            return string.Empty;

        return project.Name.Substring(frameworkStartIndex + 1, project.Name.Length - frameworkStartIndex - 2);
    }
    public static string GetOutputPath(this Project project) {
        var fallbackPath = Path.Combine(Path.GetDirectoryName(project.FilePath)!, "bin");
        if (string.IsNullOrEmpty(project.OutputFilePath))
            return fallbackPath;

        return Path.GetDirectoryName(project.OutputFilePath) ?? fallbackPath;
    }
    public static string GetIntermediateOutputPath(this Project project) {
        var fallbackPath = Path.Combine(Path.GetDirectoryName(project.FilePath)!, "obj");
        if (string.IsNullOrEmpty(project.OutputRefFilePath))
            return fallbackPath;

        var directory = new DirectoryInfo(Path.GetDirectoryName(project.OutputRefFilePath)!);
        if (directory.Name.Equals("ref", StringComparison.OrdinalIgnoreCase))
            return directory.Parent?.FullName ?? fallbackPath;

        return directory.FullName;
    }

    public static IEnumerable<DocumentId> GetDocumentIdsWithFolderPath(this Project project, string folderPath) {
        var folderPathFixed = folderPath.EndsWith(Path.DirectorySeparatorChar) ? folderPath : folderPath + Path.DirectorySeparatorChar;
        var filteredDocuments = project.Documents.Where(document => {
            if (document.Folders.Count > 0 && document.Folders[0].Equals("obj", StringComparison.OrdinalIgnoreCase))
                return false;
            return PathExtensions.StartsWith(document.FilePath, folderPathFixed);
        });
        
        return filteredDocuments.Select(document => document.Id);
    }
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFolderPath(this Project project, string folderPath) {
        var folderPathFixed = folderPath.EndsWith(Path.DirectorySeparatorChar) ? folderPath : folderPath + Path.DirectorySeparatorChar;
        var filteredDocuments = project.AdditionalDocuments.Where(document => {
            if (document.Folders.Count > 0 && document.Folders[0].Equals("obj", StringComparison.OrdinalIgnoreCase))
                return false;
            return PathExtensions.StartsWith(document.FilePath, folderPathFixed);
        });
        
        return filteredDocuments.Select(document => document.Id);
    }
    public static IEnumerable<DocumentId> GetDocumentIdsWithFilePath(this Project project, string? filePath) {
        return project.Documents.Where(it => PathExtensions.Equals(it.FilePath, filePath)).Select(it => it.Id);
    }
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFilePath(this Project project, string? filePath) {
        return project.AdditionalDocuments.Where(it => PathExtensions.Equals(it.FilePath, filePath)).Select(it => it.Id);
    }

    public static IEnumerable<string> GetFolders(this Project project, string documentPath) {
        var rootDirectory = Path.GetDirectoryName(project.FilePath);
        var documentDirectory = Path.GetDirectoryName(documentPath);
        if (string.IsNullOrEmpty(documentDirectory) || string.IsNullOrEmpty(rootDirectory))
            return Enumerable.Empty<string>();

        var relativePath = documentDirectory.Replace(rootDirectory, string.Empty, StringComparison.OrdinalIgnoreCase);
        return relativePath.Split(Path.DirectorySeparatorChar).Where(it => !string.IsNullOrEmpty(it));
    }

    internal static bool HasFolder(this Project project, string folderName) {
        return project.Documents.Any(it => it.Folders.Contains(folderName, StringComparer.OrdinalIgnoreCase));
    }
}
