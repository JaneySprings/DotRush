using System.Text.Json.Serialization;

namespace DotRush.Protocol.Models;

public class Command {
    public string Title { get; set; } = string.Empty;
    [JsonPropertyName("command")] public string Name { get; set; } = string.Empty;
    public List<LSPAny>? Arguments { get; set; }
}
