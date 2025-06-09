using System.Text.Json.Serialization;
using SystemPath = System.IO.Path;

namespace DotRush.Common.MSBuild;

public class MSBuildProject {
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("path")] public string Path { get; set; }
    [JsonPropertyName("frameworks")] public IEnumerable<string> Frameworks { get; set; }
    [JsonPropertyName("configurations")] public IEnumerable<string> Configurations { get; set; }
    [JsonPropertyName("isLegacyFormat")] public bool IsLegacyFormat { get; set; }

    [JsonIgnore] public string Directory => SystemPath.GetDirectoryName(Path)!;

    internal MSBuildProject(string path) {
        Frameworks = Enumerable.Empty<string>();
        Configurations = Enumerable.Empty<string>();
        Name = SystemPath.GetFileNameWithoutExtension(path);
        Path = SystemPath.GetFullPath(path);
    }

    public bool IsExecutable() {
        var outputType = this.EvaluateProperty("OutputType");
        return outputType != null && outputType.Contains("exe", StringComparison.OrdinalIgnoreCase);
    }
    public bool IsTestProject() {
        var isTestProject = this.EvaluateProperty("IsTestProject");
        if (isTestProject != null && isTestProject.Contains("true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (IsLegacyFormat)
            return false;

        return this.HasPackage("NUnit") || this.HasPackage("NUnitLite") || this.HasPackage("xunit");
    }
    public string GetAssemblyName() {
        return this.EvaluateProperty("AssemblyName", SystemPath.GetFileNameWithoutExtension(Path)) ?? string.Empty;
    }
}