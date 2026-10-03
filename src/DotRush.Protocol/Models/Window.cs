namespace DotRush.Protocol.Models;

public class ShowMessageParams {
    public MessageType Type { get; set; }
    public string Message { get; set; } = string.Empty;
}

public enum MessageType {
    Error = 1,
    Warning = 2,
    Info = 3,
    Log = 4,
    Debug = 5
}

public class WorkDoneProgressCreateParams {
    public string Token { get; set; } = string.Empty;
}

public class ProgressParams {
    public string Token { get; set; } = string.Empty;
    public object Value { get; set; } = null!;
}

public class WorkDoneProgressBegin {
    public string Kind => "begin";
    public string Title { get; set; } = string.Empty;
    public bool? Cancellable { get; set; }
    public string? Message { get; set; }
    public int? Percentage { get; set; }
}

public class WorkDoneProgressReport {
    public string Kind => "report";
    public bool? Cancellable { get; set; }
    public string? Message { get; set; }
    public int? Percentage { get; set; }
}

public class WorkDoneProgressEnd {
    public string Kind => "end";
    public string? Message { get; set; }
}
