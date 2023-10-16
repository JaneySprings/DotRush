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
        var folderPathFixed = folderPath.EndsWith(Path.DirectorySeparatorChar) ? folderPath : folderPath + Path.DirectorySeparatorChar;
        return project.Documents
            .Where(document => document.FilePath?.StartsWith(folderPathFixed, StringComparison.OrdinalIgnoreCase) == true)
            .Select(document => document.Id);
    }

    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFolderPath(this Solution solution, string folderPath) {
        return solution.Projects.SelectMany(project => project.GetAdditionalDocumentIdsWithFolderPath(folderPath));
    }
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFolderPath(this Project project, string folderPath) {
        var folderPathFixed = folderPath.EndsWith(Path.DirectorySeparatorChar) ? folderPath : folderPath + Path.DirectorySeparatorChar;
        return project.AdditionalDocuments
            .Where(document => document.FilePath?.StartsWith(folderPathFixed, StringComparison.OrdinalIgnoreCase) == true)
            .Select(document => document.Id);
    }

    public static DocumentId GetAdditionalDocumentIdWithFilePath(this Project project, string filePath) {
        return project.AdditionalDocuments.Single(document => document.FilePath == filePath).Id;
    }
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFilePath(this Solution solution, string filePath) {
        return solution.Projects.Select(project => project.GetAdditionalDocumentIdWithFilePath(filePath));
    }

    public static IEnumerable<ProjectId>? GetProjectIdsMayContainsFilePath(this Solution solution, string documentPath) {
        var projects = solution.Projects.Where(p => documentPath.StartsWith(Path.GetDirectoryName(p.FilePath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        if (!projects.Any())
            return null;

        var maxCommonFoldersCount = projects.Max(project => GetMaxCommonFoldersCount(project, documentPath));
        return projects
            .Where(project => GetMaxCommonFoldersCount(project, documentPath) == maxCommonFoldersCount)
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

    public static IEnumerable<string> GetVisibleFiles(string folder, string mask) {
        return Directory.EnumerateFiles(folder, mask, SearchOption.AllDirectories).Where(it => IsFileVisible(it, folder));
    }

    public static bool IsFileVisible(string filePath, string baseDirectory) {
        var directoryInfo = new DirectoryInfo(baseDirectory);
        if (directoryInfo.Attributes.HasFlag(FileAttributes.Hidden))
            return false;
        
        var directoryNames = filePath.Replace(baseDirectory, string.Empty).Split(Path.DirectorySeparatorChar);
        var currentProcessingDirectory = baseDirectory;
        foreach (var directoryName in directoryNames) {
            currentProcessingDirectory = Path.Combine(currentProcessingDirectory, directoryName);
            directoryInfo = new DirectoryInfo(currentProcessingDirectory);
            if (directoryInfo.Attributes.HasFlag(FileAttributes.Hidden))
                return false;
        }
        
        var fileInfo = new FileInfo(filePath);
        return !fileInfo.Attributes.HasFlag(FileAttributes.Hidden);
    }

    private static int GetMaxCommonFoldersCount(Project project, string documentPath) {
        var folders = project.GetFolders(documentPath).ToList();
        var documents = project.Documents.Where(it => it.Folders.Count <= folders.Count);
        var maxCounter = 0;
        foreach (var document in documents) {
            var counter = 0;
            for (int i = 0; i < document.Folders.Count; i++) {
                if (folders[i] != document.Folders[i])
                    break;
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