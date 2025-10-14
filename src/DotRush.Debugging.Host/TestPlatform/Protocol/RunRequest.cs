using System.Text.Json.Serialization;

namespace DotRush.Debugging.Host.TestPlatform.Protocol;

public class RunRequest {
    [JsonPropertyName("tests")] public TestNode[]? TestCases { get; set; }
    [JsonPropertyName("runId")] public Guid RunId { get; set; }

    public RunRequest(Guid runId) {
        RunId = runId;
    }
}
