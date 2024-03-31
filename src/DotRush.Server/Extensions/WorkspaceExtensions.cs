using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace DotRush.Server.Extensions;

public static class WorkspaceExtensions {
    public static IEnumerable<ProjectId> GetProjectIdsWithDocumentFilePath(this Solution solution, string filePath) {
        return solution.GetDocumentIdsWithFilePath(filePath).Select(id => id.ProjectId);
    }
    public static IEnumerable<ProjectId> GetProjectIdsWithDocumentsFilePaths(this Solution solution, IEnumerable<string> filePaths) {
        return filePaths.SelectMany(filePath => solution.GetProjectIdsWithDocumentFilePath(filePath)).Distinct();
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
    public static DocumentId? GetDocumentIdWithFilePath(this Project project, string filePath) {
        return project.Documents.FirstOrDefault(document => FileSystemExtensions.PathEquals(document.FilePath, filePath))?.Id;
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

    public static DocumentId? GetAdditionalDocumentIdWithFilePath(this Project project, string filePath) {
        return project.AdditionalDocuments.FirstOrDefault(document => FileSystemExtensions.PathEquals(document.FilePath, filePath))?.Id;
    }
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFilePath(this Solution solution, string filePath) {
        return solution.Projects.Select(project => project.GetAdditionalDocumentIdWithFilePath(filePath)).Where(it => it != null)!;
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
    public static IEnumerable<string> GetProjectFiles(IEnumerable<string>? directories) {
        if (directories == null)
            return Enumerable.Empty<string>();

        var result = new List<string>();
        foreach (var directory in directories) {
            var directoryProjectFiles = FileSystemExtensions.GetVisibleFiles(directory).Where(it => LanguageServer.IsProjectFile(it));
            if (directoryProjectFiles.Any()) {
                result.AddRange(directoryProjectFiles);
                continue;
            }
            result.AddRange(GetProjectFiles(FileSystemExtensions.GetVisibleDirectories(directory)));
        }
        return result;
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