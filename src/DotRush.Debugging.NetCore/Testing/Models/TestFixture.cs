using System.Text.Json.Serialization;
using DotRush.Common.Extensions;

namespace DotRush.Debugging.NetCore.Testing.Models;

public class TestFixture {
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("filePath")] public string FilePath { get; set; }
    [JsonPropertyName("range")] public Range? Range { get; set; }
    [JsonPropertyName("children")] public HashSet<TestCase> TestCases { get; set; }
    [JsonIgnore] public bool IsAbstract { get; set; }
    [JsonIgnore] public string? BaseFixtureName { get; set; }

    public TestFixture(string id, string name, string filePath) {
        Id = id;
        Name = name;
        FilePath = filePath;
        TestCases = new HashSet<TestCase>();
    }

    public void Resolve(IEnumerable<TestFixture> fixtures) {
        if (string.IsNullOrEmpty(BaseFixtureName)) 
            return;

        var parentFixtures = fixtures.Where(f => f.Name == BaseFixtureName);
        foreach (var parentFixture in parentFixtures) {
            if (!string.IsNullOrEmpty(parentFixture.BaseFixtureName))
                parentFixture.Resolve(fixtures);

            TestCases.AddRange(parentFixture.TestCases.Select(c => c.UpdateParent(this)));
        }

        BaseFixtureName = null;
    }
}
