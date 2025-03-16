using System.Text.Json.Serialization;

namespace DotRush.Common.MSBuild;

public class MSBuildSolutionFilter {
    [JsonPropertyName("solution")]
    public MSBuildSolution? Solution { get; set; }
}

public class MSBuildSolution {
    [JsonPropertyName("path")] public string? Path { get; set; }
    [JsonPropertyName("projects")] public List<string>? Projects { get; set; }
}