using System.Text.Json.Serialization;
using DotRush.Common.Extensions;

namespace DotRush.Debugging.NetCore.Testing.Models;

public class TestFixture {
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("filePath")] public string FilePath { get; set; }
    [JsonPropertyName("range")] public Range? Range { get; set; }
    [JsonPropertyName("children")] public HashSet<TestCase> TestCases { get; set; }
    [JsonPropertyName("childFixtures")] public HashSet<TestFixture> ChildFixtures { get; set; }
    [JsonIgnore] public bool IsAbstract { get; set; }
    [JsonIgnore] public string? BaseFixtureName { get; set; }
    [JsonIgnore] public string? ParentFixtureId { get; set; }

    public TestFixture(string id, string name, string filePath) {
        Id = id;
        Name = name;
        FilePath = filePath;
        TestCases = new HashSet<TestCase>();
        ChildFixtures = new HashSet<TestFixture>();
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
    
    public TestFixture UpdateParentId(string parentId) {
        ParentFixtureId = parentId;
        return this;
    }
}
