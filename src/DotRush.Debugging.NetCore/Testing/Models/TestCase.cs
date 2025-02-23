using System.Text.Json.Serialization;

namespace DotRush.Debugging.NetCore.Testing.Models;

public class TestCase {
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("filePath")] public string FilePath { get; set; }
    [JsonPropertyName("range")] public Range? Range { get; set; }

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
    public TestCase UpdateParent(TestFixture newParent) {
        return new TestCase($"{newParent.Id}.{Name}", Name, FilePath) { Range = Range };
    }
}
