using System.Text;
using System.Text.RegularExpressions;
using DotRush.Common.Extensions;

namespace DotRush.Common.MSBuild;

public static class MSBuildPropertyEvaluator {
    public static string? EvaluateProperty(this MSBuildProject project, string name, string? defaultValue = null) {
        var propertyMatches = project.GetPropertyMatches(project.FilePath, name);
        if (propertyMatches == null)
            return defaultValue;

        var propertyValue = project.GetPropertyValue(name, propertyMatches);
        if (string.IsNullOrEmpty(propertyValue))
            return defaultValue;

        return propertyValue;
    }
    public static bool HasPackage(this MSBuildProject project, string packageName) {
        return project.HasPackageMatches(project.FilePath, packageName);
    }

    private static MatchCollection? GetPropertyMatches(this MSBuildProject project, string projectPath, string propertyName, bool isEndPoint = false) {
        if (!File.Exists(projectPath))
            return null;

        string content = File.ReadAllText(projectPath);
        content = Regex.Replace(content, "<!--.*?-->", string.Empty, RegexOptions.Singleline);
        /* Find in current project */
        var propertyMatch = new Regex($@"<{propertyName}\s?.*>(.*?)<\/{propertyName}>\s*\n").Matches(content);
        if (propertyMatch.Count > 0)
            return propertyMatch;
        var importRegex = new Regex(@"<Import\s+Project\s*=\s*""(.*?)""");
        /* Find in imported project */
        foreach (Match importMatch in importRegex.Matches(content)) {
            var importedProjectPath = ResolveImportPath(projectPath, importMatch.Groups[1].Value);
            if (!File.Exists(importedProjectPath))
                return null;

            var importedProjectPropertyMatches = project.GetPropertyMatches(importedProjectPath, propertyName, isEndPoint);
            if (importedProjectPropertyMatches != null)
                return importedProjectPropertyMatches;
        }
        /* Already at the end of the import chain */
        if (isEndPoint)
            return null;
        /* Find in Directory.Build.props */
        var propsFile = GetDirectoryPropsPath(Path.GetDirectoryName(projectPath)!);
        if (propsFile == null)
            return null;

        return project.GetPropertyMatches(propsFile, propertyName, true);
    }
    private static bool HasPackageMatches(this MSBuildProject project, string projectPath, string packageName, bool isEndPoint = false) {
        if (!File.Exists(projectPath))
            return false;

        string content = File.ReadAllText(projectPath);
        content = Regex.Replace(content, "<!--.*?-->", string.Empty, RegexOptions.Singleline);
        /* Find in current project */
        var packageMatch = new Regex($@"<PackageReference Include=""{packageName}""\s?.*>").Matches(content);
        if (packageMatch.Count > 0)
            return true;
        var importRegex = new Regex(@"<Import\s+Project\s*=\s*""(.*?)""");
        /* Find in imported project */
        foreach (Match importMatch in importRegex.Matches(content)) {
            var importedProjectPath = ResolveImportPath(projectPath, importMatch.Groups[1].Value);
            if (!File.Exists(importedProjectPath))
                return false;

            if (project.HasPackageMatches(importedProjectPath, packageName, isEndPoint))
                return true;
        }
        var projectRefRegex = new Regex(@"<ProjectReference\s+Include\s*=\s*""(.*?)""\s?.*>");
        /* Find in project references */
        foreach (Match importMatch in projectRefRegex.Matches(content)) {
            var projectReferencePath = ResolveImportPath(projectPath, importMatch.Groups[1].Value);
            if (!File.Exists(projectReferencePath))
                return false;

            if (project.HasPackageMatches(projectReferencePath, packageName, isEndPoint))
                return true;
        }
        /* Already at the end of the import chain */
        if (isEndPoint)
            return false;
        /* Find in Directory.Build.props */
        var propsFile = GetDirectoryPropsPath(Path.GetDirectoryName(projectPath)!);
        if (propsFile == null)
            return false;

        return project.HasPackageMatches(propsFile, packageName, true);
    }

    private static string GetPropertyValue(this MSBuildProject project, string propertyName, MatchCollection matches) {
        var includeRegex = new Regex(@"\$\((?<inc>.*?)\)");
        var resultSequence = new StringBuilder();
        /* Process all property entrance */
        foreach (Match match in matches) {
            var propertyValue = match.Groups[1].Value;
            /* If property reference self */
            if (propertyValue.Contains($"$({propertyName})")) {
                propertyValue = propertyValue.Replace($"$({propertyName})", resultSequence.ToString());
                resultSequence.Clear();
            }
            /* If property reference other property */
            foreach (Match includeMatch in includeRegex.Matches(propertyValue)) {
                var includePropertyName = includeMatch.Groups["inc"].Value;
                var includePropertyValue = project.EvaluateProperty(includePropertyName);
                propertyValue = propertyValue.Replace($"$({includePropertyName})", includePropertyValue ?? "");
            }
            /* Add separator and property to builder */
            if (resultSequence.Length != 0)
                resultSequence.Append(';');
            resultSequence.Append(propertyValue);
        }
        return resultSequence.ToString();
    }
    private static string? GetDirectoryPropsPath(string workspacePath) {
        var propFiles = Directory.GetFiles(workspacePath, "Directory.Build.props", SearchOption.TopDirectoryOnly);
        if (propFiles.Length > 0)
            return propFiles[0];

        var parentDirectory = Directory.GetParent(workspacePath);
        if (parentDirectory == null)
            return null;

        return GetDirectoryPropsPath(parentDirectory.FullName);
    }
    private static string ResolveImportPath(string filePath, string importPath) {
        var thisFileDirectory = Path.GetDirectoryName(filePath)!;
        var importFilePath = importPath.Replace("$(MSBuildThisFileDirectory)", thisFileDirectory + Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(importFilePath))
            return Path.GetFullPath(importFilePath).ToPlatformPath();

        return Path.GetFullPath(Path.Combine(thisFileDirectory, importFilePath)).ToPlatformPath();
    }
}