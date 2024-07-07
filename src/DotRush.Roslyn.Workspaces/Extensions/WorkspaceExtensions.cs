using DotRush.Roslyn.Common.External;
using DotRush.Roslyn.Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using FileSystemExtensions = DotRush.Roslyn.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.Workspaces.Extensions;

public static class WorkspaceExtensions {
    public static IEnumerable<ProjectId> GetProjectIdsWithDocumentFilePath(this Solution solution, string filePath) {
        return solution.Projects.Where(project => project.GetDocumentIdsWithFilePath(filePath).Any()).Select(project => project.Id);
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
    public static IEnumerable<DocumentId> GetDocumentIdsWithFilePath(this Project project, string filePath) {
        return project.Documents.Where(it => FileSystemExtensions.PathEquals(it.FilePath, filePath)).Select(it => it.Id);
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
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFilePath(this Project project, string filePath) {
        return project.AdditionalDocuments.Where(it => FileSystemExtensions.PathEquals(it.FilePath, filePath)).Select(it => it.Id);
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
        var rootDirectory = FileSystemExtensions.NormalizePath(Path.GetDirectoryName(project.FilePath) ?? string.Empty);
        var documentDirectory = FileSystemExtensions.NormalizePath(Path.GetDirectoryName(documentPath) ?? string.Empty);
        if (string.IsNullOrEmpty(documentDirectory) || string.IsNullOrEmpty(rootDirectory))
            return Enumerable.Empty<string>();

        var rootDirectoryName = Path.GetFileName(rootDirectory);
        var rootDirectoryIndex = documentDirectory.IndexOf(rootDirectoryName, StringComparison.OrdinalIgnoreCase);
        if (rootDirectoryIndex == -1)
            return Enumerable.Empty<string>();

        var relativePath = documentDirectory.Substring(rootDirectoryIndex + rootDirectoryName.Length);
        return relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
    }

    public static async Task<ProcessResult> RestoreProjectAsync(this MSBuildWorkspace workspace, string projectPath, CancellationToken cancellationToken) {
        var processInfo = ProcessRunner.CreateProcess("dotnet", $"restore \"{projectPath}\"", captureOutput: true, displayWindow: false, cancellationToken: cancellationToken);
        var restoreResult = await processInfo.Result;

        if (restoreResult.ExitCode != 0) {
            foreach (var line in restoreResult.OutputLines)
                CurrentSessionLogger.Error(line);
            foreach (var line in restoreResult.ErrorLines)
                CurrentSessionLogger.Error(line);
        }

        return restoreResult;
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
}
