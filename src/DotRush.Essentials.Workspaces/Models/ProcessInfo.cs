using System.Diagnostics;
using System.Text.Json.Serialization;

namespace DotRush.Essentials.Workspaces.Models;

public class ProcessInfo {
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    public ProcessInfo() {}
    public ProcessInfo(Process process) {
        Id = process.Id;
        Name = process.ProcessName;
    }
}