using MediatR;

namespace DotRush.Server.Handlers;

public class FrameworkChangedParams: INotification {
    public string? framework { get; set; }
}