using Microsoft.CodeAnalysis;

namespace DotRush.Server.Extensions;

public static class WorkspaceExtensions {
    public static IEnumerable<ProjectId> GetProjectIdsWithDocumentFilePath(this Solution solution, string filePath) {
        return solution.GetDocumentIdsWithFilePath(filePath).Select(id => id.ProjectId);
    }

    public static IEnumerable<ProjectId> GetProjectIdsWithFilePath(this Solution solution, string filePath) {
        return solution.Projects.Where(project => project.FilePath == filePath).Select(project => project.Id);
    }

    public static IEnumerable<string> GetFolders(this Project project, string documentPath) {
        var rootDirectory = Path.GetDirectoryName(project.FilePath)!;
        var documentDirectory = Path.GetDirectoryName(documentPath)!;
        var relativePath = documentDirectory.Replace(rootDirectory, string.Empty);
        return relativePath.Split(Path.DirectorySeparatorChar).Where(it => !string.IsNullOrEmpty(it));
    }
}