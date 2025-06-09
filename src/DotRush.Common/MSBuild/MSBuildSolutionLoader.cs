using System.Collections.ObjectModel;
using System.Text.Json;
using System.Xml.Linq;
using DotRush.Common.Logging;

namespace DotRush.Common.MSBuild;

public static class MSBuildSolutionLoader {
    public static IEnumerable<MSBuildProject> LoadProjects(string solutionFile, Action<string>? callback = null) {
        var projectFiles = GetProjectFiles(solutionFile);
        foreach (var projectFile in projectFiles) {
            var project = MSBuildProjectsLoader.LoadProject(projectFile, callback);
            if (project == null) {
                CurrentSessionLogger.Error($"Failed to load project: {projectFile}");
                continue;
            }
            yield return project;
        }
    }
    public static ReadOnlyCollection<string> GetProjectFiles(string solutionFile) {
        try {
            switch (Path.GetExtension(solutionFile)) {
                case ".sln":
                    return GetProjectsFromClassicSolution(solutionFile);
                case ".slnx":
                    return GetProjectsFromSolutionX(solutionFile);
                case ".slnf":
                    return GetProjectsFromSolutionFilter(solutionFile);
            }
        } catch (Exception e) {
            CurrentSessionLogger.Error(e);
        }

        return ReadOnlyCollection<string>.Empty;
    }

    private static ReadOnlyCollection<string> GetProjectsFromClassicSolution(string solutionPath) {
        var projects = new List<string>();
        var solutionContent = File.ReadAllLines(solutionPath);
        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        foreach (var line in solutionContent) {
            if (!line.StartsWith("Project(", StringComparison.OrdinalIgnoreCase))
                continue;

            var lineParts = line.Split(',');
            if (lineParts.Length < 2)
                continue;
            // Skip solution folder
            if (line.Contains("{2150E333-8FDC-42A3-9474-1A3956D46DE8}", StringComparison.OrdinalIgnoreCase))
                continue;

            var projectPath = lineParts[1].Trim().Trim('"');
            if (!Path.IsPathRooted(projectPath))
                projectPath = Path.Combine(solutionDirectory, projectPath);

            projects.Add(Path.GetFullPath(projectPath));
        }

        return projects.AsReadOnly();
    }
    private static ReadOnlyCollection<string> GetProjectsFromSolutionFilter(string solutionPath) {
        var solutionFilterDirectory = Path.GetDirectoryName(solutionPath)!;
        var solutionFilter = JsonSerializer.Deserialize<MSBuildSolutionFilter>(File.ReadAllText(solutionPath));
        if (solutionFilter?.Solution == null || solutionFilter.Solution.Projects == null)
            return ReadOnlyCollection<string>.Empty;

        var projects = new List<string>();
        foreach (var path in solutionFilter.Solution.Projects) {
            var currentPath = path;
            if (!Path.IsPathRooted(currentPath))
                currentPath = Path.Combine(solutionFilterDirectory, path);

            projects.Add(Path.GetFullPath(currentPath));
        }

        return projects.AsReadOnly();
    }
    private static ReadOnlyCollection<string> GetProjectsFromSolutionX(string solutionPath) {
        var solutionXDirectory = Path.GetDirectoryName(solutionPath)!;
        var solutionX = XDocument.Load(solutionPath);

        var projects = new List<string>();
        var projectElements = solutionX.Root?.Elements("Project");
        if (projectElements == null)
            return ReadOnlyCollection<string>.Empty;

        foreach (var projectElement in projectElements) {
            var projectPath = projectElement.Attribute("Path")?.Value;
            if (string.IsNullOrWhiteSpace(projectPath))
                continue;

            if (!Path.IsPathRooted(projectPath))
                projectPath = Path.Combine(solutionXDirectory, projectPath);

            projects.Add(Path.GetFullPath(projectPath));
        }

        return projects.AsReadOnly();
    }
}