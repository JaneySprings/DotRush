using System.Text.Json;

namespace DotRush.Protocol.Models;

public class InitializeParams {
    public int? ProcessId { get; set; }
    public ClientInfo? ClientInfo { get; set; }
    public string? Locale { get; set; }
    public JsonElement? InitializationOptions { get; set; }
    public JsonElement? Capabilities { get; set; }
    public List<WorkspaceFolder>? WorkspaceFolders { get; set; }
}

public class InitializeResult {
    public ServerCapabilities Capabilities { get; set; } = null!;
    public ServerInfo? ServerInfo { get; set; }
}

public class ClientInfo {
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
}

public class ServerInfo {
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
}

public class WorkspaceFolder {
    public DocumentUri Uri { get; set; }
    public string Name { get; set; } = string.Empty;
}
