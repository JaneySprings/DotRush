using Microsoft.CodeAnalysis;

namespace DotRush.Server.Extensions;

public static class WorkspaceExtensions {
    public static IEnumerable<ProjectId> GetProjectIdsWithDocumentFilePath(this Solution solution, string filePath) {
        return solution.GetDocumentIdsWithFilePath(filePath).Select(id => id.ProjectId);
    }

    public static IEnumerable<DocumentId> GetDocumentIdsWithFolderPath(this Solution solution, string folderPath) {
        return solution.Projects.SelectMany(project => project.GetDocumentIdsWithFolderPath(folderPath));
    }
    public static IEnumerable<DocumentId> GetDocumentIdsWithFolderPath(this Project project, string folderPath) {
        return project.Documents
            .Where(document => document.FilePath?.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase) == true)
            .Select(document => document.Id);
    }

    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFolderPath(this Solution solution, string folderPath) {
        return solution.Projects.SelectMany(project => project.GetAdditionalDocumentIdsWithFolderPath(folderPath));
    }
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFolderPath(this Project project, string folderPath) {
        return project.AdditionalDocuments
            .Where(document => document.FilePath?.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase) == true)
            .Select(document => document.Id);
    }

    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFilePath(this Project project, string filePath) {
        return project.AdditionalDocuments
            .Where(document => document.FilePath == filePath)
            .Select(document => document.Id);
    }
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFilePath(this Solution solution, string filePath) {
        return solution.Projects.SelectMany(project => project.GetAdditionalDocumentIdsWithFilePath(filePath));
    }

    public static IEnumerable<ProjectId>? GetProjectIdsMayContainsFilePath(this Solution solution, string documentPath) {
        var projects = solution.Projects.Where(p => documentPath.StartsWith(Path.GetDirectoryName(p.FilePath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

        if (!projects.Any())
            return null;

        var maxCommonFoldersCount = projects.Max(project => GetCommonFoldersCount(project, documentPath));
        return projects
            .Where(project => GetCommonFoldersCount(project, documentPath) == maxCommonFoldersCount)
            .Select(project => project.Id);
    }

    public static IEnumerable<string> GetFolders(this Project project, string documentPath) {
        var rootDirectory = Path.GetDirectoryName(project.FilePath);
        var documentDirectory = Path.GetDirectoryName(documentPath);
        if (documentDirectory == null || rootDirectory == null)
            return Enumerable.Empty<string>();

        var relativePath = documentDirectory.Replace(rootDirectory, string.Empty);
        return relativePath.Split(Path.DirectorySeparatorChar).Where(it => !string.IsNullOrEmpty(it));
    }

    public static bool ContainsProjectsWithPath(this Workspace? workspace, string projectPath) {
        return workspace?.CurrentSolution.Projects.Any(project => project.FilePath == projectPath) == true;
    }

    private static int GetCommonFoldersCount(Project project, string documentPath) {
        var folders = project.GetFolders(documentPath);
        var maxCounter = 0;

        foreach (var document in project.Documents) {
            var counter = 0;
            foreach (var folder in folders) {
                if (document.Folders.Contains(folder))
                    counter++;
            }
            if (counter > maxCounter)
                maxCounter = counter;
        }

        return maxCounter;
    }
}