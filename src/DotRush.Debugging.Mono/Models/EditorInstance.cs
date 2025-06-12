using System.Text.Json.Serialization;

namespace DotRush.Debugging.Mono.Models;

public class EditorInstance {
    [JsonPropertyName("process_id")] public int ProcessId { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("app_path")] public string? AppPath { get; set; }
    [JsonPropertyName("app_contents_path")] public string? AppContentsPath { get; set; }
}