using System.Text.Json.Serialization;

namespace DotRush.Common.MSBuild;

public class MSBuildProject {
    [JsonPropertyName("name")] public string Name { get; init; }
    [JsonPropertyName("path")] public string FilePath { get; init; }
    [JsonPropertyName("directory")] public string? DirectoryPath { get; init; }
    [JsonPropertyName("frameworks")] public IEnumerable<string> Frameworks { get; set; }
    [JsonPropertyName("configurations")] public IEnumerable<string> Configurations { get; set; }
    [JsonPropertyName("isLegacyFormat")] public bool IsLegacyFormat { get; set; }
    [JsonPropertyName("isTestProject")] public bool IsTestProject { get; set; }

    internal MSBuildProject(string path) {
        Name = Path.GetFileNameWithoutExtension(path);
        FilePath = Path.GetFullPath(path);
        DirectoryPath = Path.GetDirectoryName(FilePath);
        Frameworks = Enumerable.Empty<string>();
        Configurations = Enumerable.Empty<string>();
    }

    public string GetAssemblyName() {
        return this.EvaluateProperty("AssemblyName", Path.GetFileNameWithoutExtension(FilePath)) ?? string.Empty;
    }
}