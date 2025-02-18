using System.Text.Json.Serialization;

namespace DotRush.Debugging.NetCore.Models;

public class PackageInfo {
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("runtimeDependencies")]
    public IEnumerable<RuntimeDependencyInfo>? RuntimeDependencies { get; set; }
}

public class RuntimeDependencyInfo {
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("platforms")]
    public IEnumerable<string> Platforms { get; set; } = Enumerable.Empty<string>();

    [JsonPropertyName("architectures")]
    public IEnumerable<string> Architectures { get; set; } = Enumerable.Empty<string>();

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}