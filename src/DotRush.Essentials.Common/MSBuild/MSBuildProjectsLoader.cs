namespace DotRush.Essentials.Common.MSBuild;

public static class MSBuildProjectsLoader {
    public static MSBuildProject? LoadProject(string projectFile, Action<string>? callback = null) {
        if (!File.Exists(projectFile)) {
            callback?.Invoke($"Could not find workspace directory {projectFile}");
            return null;
        }

        var project = new MSBuildProject(projectFile);
        project.Configurations = GetConfigurations(project);
        project.Frameworks = GetTargetFrameworks(project);
        project.IsTestProject = IsTestProject(project);
        project.IsExecutable = IsProjectExecutable(project);

        return project;
    }

    private static bool IsProjectExecutable(MSBuildProject project) {
        var outputType = project.EvaluateProperty("OutputType");
        return outputType != null && outputType.Contains("exe", StringComparison.OrdinalIgnoreCase);
    }
    private static bool IsTestProject(MSBuildProject project) {
        var isTestProject = project.EvaluateProperty("IsTestProject");
        return isTestProject != null && isTestProject.Contains("true", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetTargetFrameworks(MSBuildProject project) {
        var frameworks = new List<string>();

        var singleFramework = project.EvaluateProperty("TargetFramework");
        if (!string.IsNullOrEmpty(singleFramework)) {
            frameworks.Add(singleFramework);
            return frameworks;
        }

        var multipleFrameworks = project.EvaluateProperty("TargetFrameworks");
        if (!string.IsNullOrEmpty(multipleFrameworks)) {
            foreach (var framework in multipleFrameworks.Split(';')) {
                if (frameworks.Contains(framework) || string.IsNullOrEmpty(framework))
                    continue;
                frameworks.Add(framework);
            }
            return frameworks;
        }

        return frameworks;
    }
    private static IEnumerable<string> GetConfigurations(MSBuildProject project) {
        var configurations = new List<string>();

        var configurationsRaw = project.EvaluateProperty("Configurations", string.Empty) + ";Debug;Release";
        foreach (var configuration in configurationsRaw.Split(';')) {
            if (configurations.Contains(configuration) || string.IsNullOrEmpty(configuration))
                continue;
            configurations.Add(configuration);
        }

        return configurations.OrderBy(x => x).ToArray();
    }
}