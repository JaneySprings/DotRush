using System.Text.Json.Serialization;

namespace DotRush.Debugging.Host;

public class Status {
    [JsonPropertyName("isSuccess")] public bool IsSuccess { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("payload")] public object? Payload { get; set; }

    public Status(bool isSuccess, string? message, object? payload = null) {
        IsSuccess = isSuccess;
        Message = message;
        Payload = payload;
    }

    public static Status Success(string? message = null, object? payload = null) => new(true, message, payload);
    public static Status Fail(string message) => new(false, message);
}