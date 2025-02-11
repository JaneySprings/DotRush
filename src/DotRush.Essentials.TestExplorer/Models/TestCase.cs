using System.Text.Json.Serialization;

namespace DotRush.Essentials.TestExplorer.Models;

public class TestFixture {

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; }

    [JsonPropertyName("range")]
    public Range? Range { get; set; }

    [JsonPropertyName("children")]
    public HashSet<TestCase> Children { get; set; }

    public TestFixture(string id, string name, string filePath) {
        Id = id;
        Name = name;
        FilePath = filePath;
        Children = new HashSet<TestCase>();
    }

    public override int GetHashCode() {
        return Id.GetHashCode();
    }
    public override bool Equals(object? obj) {
        if (obj is TestFixture other)
            return Id == other.Id;
        return false;
    }
}

public class TestCase {

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; }

    [JsonPropertyName("range")]
    public Range? Range { get; set; }

    public TestCase(string id, string name, string filePath) {
        Id = id;
        Name = name;
        FilePath = filePath;
    }

    public override int GetHashCode() {
        return Id.GetHashCode();
    }
    public override bool Equals(object? obj) {
        if (obj is TestCase other)
            return Id == other.Id;
        return false;
    }
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