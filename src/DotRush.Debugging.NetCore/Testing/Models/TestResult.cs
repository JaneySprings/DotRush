using System.Text.Json.Serialization;

namespace DotRush.Debugging.NetCore.Testing.Models;

public class TestResult {

    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("state")] // Passed, Failed, Skipped
    public string? State { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("stackTrace")]
    public string? StackTrace { get; set; }
}
