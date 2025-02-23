using System.Text.Json.Serialization;

namespace DotRush.Debugging.NetCore.Testing.Models;

public class Range {
    [JsonPropertyName("start")]
    public Position? Start { get; set; }

    [JsonPropertyName("end")]
    public Position? End { get; set; }
}

public class Position {
    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("character")]
    public int Character { get; set; }
}