using System.Text.Json.Serialization;

namespace DotRush.Essentials.TestExplorer.Models;

public class TestCase {

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    [JsonPropertyName("range")]
    public Range? Range { get; set; }

    [JsonPropertyName("children")]
    public IEnumerable<TestCase>? Children { get; set; }
}

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