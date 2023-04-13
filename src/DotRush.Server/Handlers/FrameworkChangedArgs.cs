using LanguageServer;

namespace DotRush.Server.Handlers;

public class FrameworkChangedArgs: NotificationMessageBase {
    public FrameworkChangedArgs() {}

    public FrameworkParams? @params { get; set; }
}

public class FrameworkParams {
    public FrameworkParams() {}

    public string? framework { get; set; }
}