using System.Diagnostics;
using System.Text.Json.Serialization;
using DotRush.Common.Extensions;

namespace DotRush.Debugging.NetCore.Models;

public class ProcessInfo {
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("startTime")]
    public string? StartTime { get; set; }

    public ProcessInfo() { }
    public ProcessInfo(Process process) {
        Id = process.Id;
        Name = process.ProcessName;
        StartTime = SafeExtensions.Invoke(() => process.StartTime.ToShortTimeString());
    }
}