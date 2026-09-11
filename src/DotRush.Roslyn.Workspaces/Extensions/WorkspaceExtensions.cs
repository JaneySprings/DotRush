using DotRush.Common.Extensions;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Workspaces.Extensions;

public static class WorkspaceExtensions {
    private static readonly string[] sourceCodeExtensions = { ".cs", /* .fs .vb */};
    private static readonly string[] additionalDocumentExtensions = { ".xaml", /* maybe '.razor' ? */};
    private static readonly string[] compilerGeneratedExtensions = { ".g.cs", ".sg.cs" };
    private static readonly string[] projectFileExtensions = { ".csproj", /* fsproj vbproj */};
    private static readonly string[] solutionFileExtensions = { ".sln", ".slnf", ".slnx" };

    public static bool IsSourceCodeDocument(string filePath) {
        return !IsCompilerGeneratedDocument(filePath) && sourceCodeExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsAdditionalDocument(string filePath) {
        return additionalDocumentExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsRelevantDocument(string filePath) {
        return IsSourceCodeDocument(filePath) || IsAdditionalDocument(filePath);
    }
    public static bool IsProjectFile(string filePath) {
        return projectFileExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsSolutionFile(string filePath) {
        return solutionFileExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsCompilerGeneratedDocument(string filePath) {
        return compilerGeneratedExtensions.Any(it => filePath.EndsWith(it, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<DocumentId> GetDocumentIdsWithFilePathV2(this Solution solution, string? filePath) {
        var documentIds = solution.GetIndexedDocumentIds(filePath).Where(id => solution.GetDocument(id) != null).ToArray();
        if (documentIds.Length != 0)
            return documentIds;
        return solution.Projects.SelectMany(it => it.GetDocumentIdsWithFilePath(filePath));
    }
    public static IEnumerable<DocumentId> GetAdditionalDocumentIdsWithFilePathV2(this Solution solution, string? filePath) {
        var documentIds = solution.GetIndexedDocumentIds(filePath).Where(id => solution.GetAdditionalDocument(id) != null).ToArray();
        if (documentIds.Length != 0)
            return documentIds;
        return solution.Projects.SelectMany(it => it.GetAdditionalDocumentIdsWithFilePath(filePath));
    }
    public static IEnumerable<Document> GetDocumentsWithDirectoryPath(this Solution solution, string? filePath) {
        return solution.Projects.SelectMany(it => it.GetDocumentsWithDirectoryPath(filePath));
    }
    public static IEnumerable<TextDocument> GetAdditionalDocumentsWithDirectoryPath(this Solution solution, string? filePath) {
        return solution.Projects.SelectMany(it => it.GetAdditionalDocumentsWithDirectoryPath(filePath));
    }
    public static Document[] GetDocuments(this Solution solution, IEnumerable<DocumentId>? documentIds) {
        if (documentIds == null || !documentIds.Any())
            return Array.Empty<Document>();

        return documentIds.Select(documentId => solution.GetDocument(documentId)).OfType<Document>().ToArray();
    }
    internal static string? GetFullPath(string baseDirectory, string path) {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        path = path.ToPlatformPath();
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path));
    }

    // Documents are registered with their full path, so the solution's own index can be used before falling back to a scan.
    // Results keep the project order (same as a scan would), handlers rely on it when merging per-project results.
    private static IEnumerable<DocumentId> GetIndexedDocumentIds(this Solution solution, string? filePath) {
        if (string.IsNullOrEmpty(filePath))
            return Enumerable.Empty<DocumentId>();

        string fullPath;
        try {
            fullPath = Path.GetFullPath(filePath.ToPlatformPath());
        } catch {
            return Enumerable.Empty<DocumentId>();
        }

        var documentIds = solution.GetDocumentIdsWithFilePath(fullPath);
        if (documentIds.Length <= 1)
            return documentIds;

        var projectOrder = solution.ProjectIds.Select((id, index) => (id, index)).ToDictionary(it => it.id, it => it.index);
        return documentIds.OrderBy(id => projectOrder.GetValueOrDefault(id.ProjectId, int.MaxValue));
    }
}
