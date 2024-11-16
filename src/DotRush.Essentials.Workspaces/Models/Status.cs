
namespace DotRush.Essentials.Workspaces.Models;

public class Status {
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }

    public Status(bool isSuccess, string? message) {
        IsSuccess = isSuccess;
        Message = message;
    }

    public static Status Success(string? message = null) => new(true, message);
    public static Status Fail(string message) => new(false, message);
}