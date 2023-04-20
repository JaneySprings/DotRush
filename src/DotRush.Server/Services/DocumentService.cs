using DotRush.Server.Extensions;
using LanguageServer.Parameters.TextDocument;
using LanguageServer.Parameters.Workspace;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Server.Services;

public class DocumentService {
    public static Document? GetDocumentByPath(string? path) {
        if (string.IsNullOrEmpty(path)) 
            return null;
        var documentId = SolutionService.Instance.Solution?
            .GetDocumentIdsWithFilePath(path)
            .FirstOrDefault();
        return SolutionService.Instance.Solution?.GetDocument(documentId);
    }
    public static IEnumerable<Document>? GetDocumentsByDirectoryPath(string? path) {
        if (string.IsNullOrEmpty(path)) 
            return null;

        if (!path.EndsWith(Path.DirectorySeparatorChar))
            path += Path.DirectorySeparatorChar;
        
        var project = GetProjectByDocumentPath(path);
        if (project == null) 
            return null;

        return project.Documents.Where(it => it.FilePath!.StartsWith(path));
    }
    public static Project? GetProjectByDocumentPath(string path) {
        var relativeProjectPath = SolutionService.Instance.ProjectFiles?
            .FirstOrDefault(it => path.Contains(Path.GetDirectoryName(it)!));
        if (relativeProjectPath == null) 
            return null;

        var relativeFilePath = path.Replace(Path.GetDirectoryName(relativeProjectPath) + Path.DirectorySeparatorChar, string.Empty);
        if (!ValidatePath(relativeFilePath)) 
            return null;

        return SolutionService.Instance.Solution?.Projects.FirstOrDefault(it => it.FilePath == relativeProjectPath);
    }

    public static void ApplyTextChanges(DidChangeTextDocumentParams parameters) {
        var document = GetDocumentByPath(parameters.textDocument.uri.ToSystemPath());
        if (document == null) 
            return;

        //TODO: Incremental sync (unstable)
        // var originText = document.GetTextAsync().Result;
        // var newText = originText.WithChanges(parameters.contentChanges.Select(change => {
        //     var start = change.range.start.ToOffset(document);
        //     var end = change.range.end.ToOffset(document);
        //     return new TextChange(TextSpan.FromBounds(start, end), change.text);
        // }));
        var updatedDocument = document.WithText(SourceText.From(parameters.contentChanges[0].text));
        SolutionService.Instance.UpdateSolution(updatedDocument.Project.Solution);
    }

    public static void ApplyChanges(DidChangeWatchedFilesParams parameters) {
        foreach (var change in parameters.changes) {
            if (change.type != FileChangeType.Created && change.type != FileChangeType.Deleted) 
                continue;

            var project = GetProjectByDocumentPath(change.uri.ToSystemPath());
            if (project == null) 
                continue;

            var path = change.uri.ToSystemPath();

            if (Directory.Exists(path) && change.type == FileChangeType.Created) {
                var documents = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
                foreach (var document in documents) 
                    project = CreateFile(document, project);

                continue;
            }

            if (!path.EndsWith(".cs") && change.type == FileChangeType.Deleted) {
                var documents = GetDocumentsByDirectoryPath(path)?.Select(it => it.FilePath!);
                if (documents == null) 
                    continue;

                foreach (var document in documents) 
                    project = DeleteFile(document, project);

                continue;
            }

            if (change.type == FileChangeType.Created)
                project = CreateFile(path, project);
            if (change.type == FileChangeType.Deleted)
                project = DeleteFile(path, project);
        }
    }

    private static Project CreateFile(string path, Project project) {
        var documentContent = File.ReadAllText(path);
        var updates = project.AddDocument(Path.GetFileName(path), documentContent, null, path);
        SolutionService.Instance.UpdateSolution(updates.Project.Solution);
        return updates.Project;
    }
    private static Project DeleteFile(string path, Project project) {
        var document = GetDocumentByPath(path);
        var updates = project.RemoveDocument(document!.Id);
        SolutionService.Instance.UpdateSolution(updates.Solution);
        return updates;
    }
    private static bool ValidatePath(string path) {
        var fileDirectoryTokens = path.Split(Path.DirectorySeparatorChar);
        if (fileDirectoryTokens.FirstOrDefault(it => it.StartsWith(".")) != null || 
            fileDirectoryTokens.Contains("bin") || fileDirectoryTokens.Contains("obj")) 
            return false;

        return true;
    }
}