using System.Text.Json.Serialization;
using SystemPath = System.IO.Path;

namespace DotRush.Essentials.Common.MSBuild;

public class MSBuildProject {
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("path")] public string Path { get; set; }
    [JsonPropertyName("frameworks")] public IEnumerable<string> Frameworks { get; set; }
    [JsonPropertyName("configurations")] public IEnumerable<string> Configurations { get; set; }
    [JsonPropertyName("isTestProject")] public bool IsTestProject { get; set; }
    [JsonPropertyName("isExecutable")] public bool IsExecutable { get; set; }
    [JsonPropertyName("isLegacyFormat")] public bool IsLegacyFormat { get; set; }

    [JsonIgnore] public string Directory => SystemPath.GetDirectoryName(Path)!;

    internal MSBuildProject(string path) {
        Frameworks = Enumerable.Empty<string>();
        Configurations = Enumerable.Empty<string>();
        Name = SystemPath.GetFileNameWithoutExtension(path);
        Path = SystemPath.GetFullPath(path);
    }
}