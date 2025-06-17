using DotRush.Common.Extensions;
using DotRush.Common.InteropV2;
using DotRush.Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotRush.Roslyn.Workspaces.Extensions;

public static class WorkspaceExtensions {
    private static readonly string[] sourceCodeExtensions = { ".cs", /* .fs .vb */};
    private static readonly string[] additionalDocumentExtensions = { ".xaml", /* maybe '.razor' ? */};
    private static readonly string[] compilerGeneratedExtensions = { ".g.cs", ".sg.cs" };
    private static readonly string[] projectFileExtensions = { ".csproj", /* fsproj vbproj */};
    private static readonly string[] solutionFileExtensions = { ".sln", ".slnf", ".slnx" };
    private static readonly string[] supportedSolutionExtensions = { ".sln", ".slnf" }; //slnx is not supported by Roslyn for now
    private static readonly string[] relevantExtensions = sourceCodeExtensions.Concat(additionalDocumentExtensions).ToArray();

    public static bool IsSourceCodeDocument(string filePath) {
        return sourceCodeExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsAdditionalDocument(string filePath) {
        return additionalDocumentExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsRelevantDocument(string filePath) {
        return relevantExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsProjectFile(string filePath) {
        return projectFileExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsSolutionFile(string filePath) {
        return solutionFileExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsSupportedSolutionFile(string filePath) {
        return supportedSolutionExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsCompilerGeneratedDocument(string filePath) {
        return compilerGeneratedExtensions.Any(it => filePath.EndsWith(it, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<ProjectId> GetProjectIdsMayContainsFilePath(this Solution solution, string documentPath) {
        var projects = solution.Projects.Where(p => PathExtensions.StartsWith(documentPath, Path.GetDirectoryName(p.FilePath) + Path.DirectorySeparatorChar)).ToList();
        if (projects.Count == 0 || string.IsNullOrEmpty(documentPath))
            return Enumerable.Empty<ProjectId>();
        if (projects.Count == 1)
            return projects.Select(p => p.Id);

        var directoryInfo = new DirectoryInfo(Path.GetDirectoryName(documentPath)!);
        while (directoryInfo != null) {
            var filteredProjects = projects.Where(p => p.Documents.Any(d => PathExtensions.StartsWith(d.FilePath, directoryInfo.FullName))).ToList();
            if (filteredProjects.Count != 0)
                return filteredProjects.Select(p => p.Id);

            directoryInfo = directoryInfo.Parent;
        }

        return Enumerable.Empty<ProjectId>();
    }

    public static IEnumerable<DocumentId> GetDocumentIdsWithFilePathV2(this Solution solution, string? filePath) {
        return solution.Projects.SelectMany(it => it.GetDocumentIdsWithFilePath(filePath)) ?? Enumerable.Empty<DocumentId>();
    }
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFilePathV2(this Solution solution, string? filePath) {
        return solution.Projects.SelectMany(it => it.GetAdditionalDocumentIdsWithFilePath(filePath)) ?? Enumerable.Empty<DocumentId>();
    }

    public static async Task<ProcessResult> RestoreProjectAsync(this MSBuildWorkspace workspace, string projectPath, CancellationToken cancellationToken) {
        var processInfo = ProcessRunner.CreateProcess("dotnet", $"restore \"{projectPath}\"", captureOutput: true, displayWindow: false, cancellationToken: cancellationToken);
        var restoreResult = await processInfo.Task;

        if (restoreResult.ExitCode != 0) {
            foreach (var line in restoreResult.OutputLines)
                CurrentSessionLogger.Error(line);
            foreach (var line in restoreResult.ErrorLines)
                CurrentSessionLogger.Error(line);
        }

        return restoreResult;
    }
}
