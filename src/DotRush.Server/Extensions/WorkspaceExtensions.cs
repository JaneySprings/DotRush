using Microsoft.CodeAnalysis;

namespace DotRush.Server.Extensions;

public static class WorkspaceExtensions {
    public static IEnumerable<ProjectId> GetProjectIdsWithDocumentFilePath(this Solution solution, string filePath) {
        return solution.GetDocumentIdsWithFilePath(filePath).Select(id => id.ProjectId);
    }

    public static IEnumerable<ProjectId>? GetNearestProjectIdsWithPath(this Solution solution, string path) {
        return solution.Projects
            .Where(it => path.StartsWith(Path.GetDirectoryName(it.FilePath) + Path.DirectorySeparatorChar))
            .Select(it => it.Id);
    }
}