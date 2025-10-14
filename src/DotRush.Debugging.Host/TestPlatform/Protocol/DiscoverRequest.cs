using System.Text.Json.Serialization;

namespace DotRush.Debugging.Host.TestPlatform.Protocol;

public class DiscoverRequest {
    [JsonPropertyName("runId")] public Guid RunId { get; }

    public DiscoverRequest(Guid runId) {
        RunId = runId;
    }
}
