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
    public static string GetProjectDirectory(this Project project) {
        return Path.GetDirectoryName(project.FilePath) ?? string.Empty;
    }

    public static IEnumerable<Document> GetDocumentWithFilePath(this Project project, string? filePath) {
        return project.Documents.Where(it => PathExtensions.Equals(it.FilePath, filePath));
    }
    public static IEnumerable<TextDocument> GetAdditionalDocumentWithFilePath(this Project project, string? filePath) {
        return project.AdditionalDocuments.Where(it => PathExtensions.Equals(it.FilePath, filePath));
    }
    public static IEnumerable<DocumentId> GetDocumentIdsWithFilePath(this Project project, string? filePath) {
        return project.GetDocumentWithFilePath(filePath).Select(it => it.Id);
    }
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFilePath(this Project project, string? filePath) {
        return project.GetAdditionalDocumentWithFilePath(filePath).Select(it => it.Id);
    }

    public static IEnumerable<string> GetFolders(this Project project, string documentPath) {
        var rootDirectory = Path.GetDirectoryName(project.FilePath);
        var documentDirectory = Path.GetDirectoryName(documentPath);
        if (string.IsNullOrEmpty(documentDirectory) || string.IsNullOrEmpty(rootDirectory))
            return Enumerable.Empty<string>();

        var relativePath = documentDirectory.Replace(rootDirectory, string.Empty, StringComparison.OrdinalIgnoreCase);
        return relativePath.Split(Path.DirectorySeparatorChar).Where(it => !string.IsNullOrEmpty(it));
    }
}

public class ProjectByPathComparer : IEqualityComparer<Project> {
    public static readonly ProjectByPathComparer Instance = new ProjectByPathComparer();

    private ProjectByPathComparer() { }

    public bool Equals(Project? x, Project? y) {
        return x?.FilePath == y?.FilePath;
    }
    public int GetHashCode(Project obj) {
        return obj.FilePath?.GetHashCode() ?? obj.GetHashCode();
    }
}
