using System.Text.Json.Serialization;

namespace DotRush.Debugging.NetCore.Models;

public class Status {
    [JsonPropertyName("isSuccess")] public bool IsSuccess { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }

    public Status(bool isSuccess, string? message) {
        IsSuccess = isSuccess;
        Message = message;
    }

    public static Status Success(string? message = null) => new(true, message);
    public static Status Fail(string message) => new(false, message);
}