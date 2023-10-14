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
        if (maxCommonFoldersCount < 0)
            return null;
    
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
    public static string GetOutputPath(this Project project) {
        return FirstFolderOrDefault(project.FilePath, project.OutputFilePath, $"bin{Path.DirectorySeparatorChar}");
    }
    public static string GetIntermediateOutputPath(this Project project) {
        return FirstFolderOrDefault(project.FilePath, project.OutputRefFilePath, $"obj{Path.DirectorySeparatorChar}");
    }

    public static bool ContainsProjectsWithPath(this Workspace? workspace, string projectPath) {
        return workspace?.CurrentSolution.Projects.Any(project => project.FilePath == projectPath) == true;
    }

    public static IEnumerable<string> GetFilesFromVisibleFolders(string folder, string mask) {
        return new DirectoryInfo(folder)
            .GetFiles(mask, SearchOption.AllDirectories)
            .Where(it => !it.Attributes.HasFlag(FileAttributes.Hidden))
            .Select(it => it.FullName);
    }

    private static int GetCommonFoldersCount(Project project, string documentPath) {
        var folders = project.GetFolders(documentPath);
        if (folders.Any(it => it.StartsWith(".")))
            return -1;

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
    private static string FirstFolderOrDefault(string? projectPath, string? targetPath, string fallbackFolder) {
        var projectDirectory = Path.GetDirectoryName(projectPath) + Path.DirectorySeparatorChar;
        if (targetPath == null || !targetPath.StartsWith(projectDirectory))
            return Path.Combine(projectDirectory, fallbackFolder);

        var relativePath = targetPath.Replace(projectDirectory, string.Empty);
        if (string.IsNullOrEmpty(relativePath))
            return Path.Combine(projectDirectory, fallbackFolder);

        return projectDirectory + relativePath.Split(Path.DirectorySeparatorChar).First() + Path.DirectorySeparatorChar;
    }
}