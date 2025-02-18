using DotRush.Common.Extensions;
using DotRush.Common.External;
using DotRush.Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotRush.Roslyn.Workspaces.Extensions;

public static class WorkspaceExtensions {
    public static IEnumerable<ProjectId> GetProjectIdsWithFilePath(this Solution solution, string filePath) {
        return solution.GetProjectIdsWithDocumentFilePath(filePath).Concat(solution.GetProjectIdsWithAdditionalDocumentFilePath(filePath)).Distinct();
    }
    public static IEnumerable<ProjectId> GetProjectIdsWithDocumentFilePath(this Solution solution, string filePath) {
        return solution.Projects.Where(project => project.GetDocumentIdsWithFilePath(filePath).Any()).Select(project => project.Id);
    }
    public static IEnumerable<ProjectId> GetProjectIdsWithAdditionalDocumentFilePath(this Solution solution, string filePath) {
        return solution.Projects.Where(project => project.GetAdditionalDocumentIdsWithFilePath(filePath).Any()).Select(project => project.Id);
    }
    public static IEnumerable<DocumentId> GetDocumentIdsWithFolderPath(this Solution solution, string folderPath) {
        return solution.Projects.SelectMany(project => project.GetDocumentIdsWithFolderPath(folderPath));
    }
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFolderPath(this Solution solution, string folderPath) {
        return solution.Projects.SelectMany(project => project.GetAdditionalDocumentIdsWithFolderPath(folderPath));
    }
    public static IEnumerable<ProjectId> GetProjectIdsMayContainsFilePath(this Solution solution, string documentPath) {
        var projects = solution.Projects.Where(p => PathExtensions.StartsWith(documentPath, Path.GetDirectoryName(p.FilePath) + Path.DirectorySeparatorChar));
        if (!projects.Any())
            return Enumerable.Empty<ProjectId>();

        var documentFolders = projects.First().GetFolders(documentPath); // 'First()' - Implementation uses only project file path
        var filteredProjects = projects.ToList();
        foreach (var documentFolder in documentFolders) { 
            if (filteredProjects.Count == 0)
                break;
            filteredProjects = filteredProjects.Where(p => p.HasFolder(documentFolder)).ToList();
        }

        if (filteredProjects.Count > 0)
            return filteredProjects.Select(p => p.Id);
            
        return projects.Select(p => p.Id);
    }
    
    public static IEnumerable<DocumentId> GetDocumentIdsWithFilePathV2(this Solution solution, string? filePath) {
        return solution.Projects.SelectMany(it => it.GetDocumentIdsWithFilePath(filePath)) ?? Enumerable.Empty<DocumentId>();
    }
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFilePathV2(this Solution solution, string? filePath) {
        return solution.Projects.SelectMany(it => it.GetAdditionalDocumentIdsWithFilePath(filePath)) ?? Enumerable.Empty<DocumentId>();
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
}
