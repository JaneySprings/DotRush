using System.Xml.Linq;

namespace DotRush.Common.MSBuild;

public static class MSBuildProjectsLoader {
    public static MSBuildProject? LoadProject(string? projectFile, bool resolveAdditionalProperties = false) {
        if (!File.Exists(projectFile))
            return null;

        var project = new MSBuildProject(projectFile);
        project.Configurations = GetConfigurations(project);
        project.Frameworks = GetTargetFrameworks(project);

        if (resolveAdditionalProperties) {
            project.IsLegacyFormat = IsLegacyFormat(project);
            project.IsTestProject = IsTestProject(project);
        }

        return project;
    }

    private static bool IsLegacyFormat(MSBuildProject project) {
        var document = XDocument.Load(project.FilePath);
        if (document.Root == null || !document.Root.HasAttributes)
            return false;

        return !document.Root.Attributes().Any(a => a.Name.LocalName == "Sdk");
    }
    private static bool IsExecutable(MSBuildProject project) {
        var outputType = project.EvaluateProperty("OutputType");
        return outputType != null && outputType.Contains("exe", StringComparison.OrdinalIgnoreCase);
    }
    private static bool IsTestProject(MSBuildProject project) {
        var isTestProject = project.EvaluateProperty("IsTestProject");
        if (isTestProject != null && isTestProject.Contains("true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (project.IsLegacyFormat)
            return false;

        return project.HasPackage("Microsoft.NET.Test.Sdk")
            || project.HasPackage("NUnit")
            || project.HasPackage("NUnitLite")
            || project.HasPackage("xunit")
            || project.HasPackage("MSTest")
            || project.HasPackage("TUnit")
            || project.HasPackage("gdUnit4.api");
    }

    private static IEnumerable<string> GetTargetFrameworks(MSBuildProject project) {
        var frameworkProperty = project.EvaluateProperty("TargetFramework");
        if (string.IsNullOrEmpty(frameworkProperty)) {
            frameworkProperty = project.EvaluateProperty("TargetFrameworks");
            if (string.IsNullOrEmpty(frameworkProperty))
                return Enumerable.Empty<string>();
        }

        var frameworks = new List<string>();
        foreach (var framework in frameworkProperty.Split(';')) {
            if (frameworks.Contains(framework) || string.IsNullOrEmpty(framework))
                continue;
            frameworks.Add(framework);
        }

        return frameworks;
    }
    private static List<string> GetConfigurations(MSBuildProject project) {
        var configurations = new List<string>();

        var configurationsRaw = project.EvaluateProperty("Configurations", string.Empty) + ";Debug;Release";
        foreach (var configuration in configurationsRaw.Split(';')) {
            if (configurations.Contains(configuration) || string.IsNullOrEmpty(configuration))
                continue;
            configurations.Add(configuration);
        }

        return configurations.OrderBy(x => x).ToList();
    }
}