using System.Reflection;
using System.Text.Json.Serialization;

namespace DotRush.Debugging.Host.TestPlatform.Protocol;

public class InitializeRequest {
    [JsonPropertyName("processId")] public int ProcessId { get; set; }
    [JsonPropertyName("clientInfo")] public ClientInfo? ClientInfo { get; set; }
    [JsonPropertyName("capabilities")] public ClientCapabilities? Capabilities { get; set; }

    public InitializeRequest(int processId) {
        ProcessId = processId;
        ClientInfo = new ClientInfo(Assembly.GetExecutingAssembly());
        Capabilities = new ClientCapabilities(debuggerProvider: false);
    }
}

public class ClientInfo {
    public ClientInfo(Assembly assembly) {
        Version = assembly.GetName().Version?.ToString() ?? string.Empty;
        Name = assembly.GetName().Name ?? string.Empty;
    }
    public ClientInfo(string id, string version) {
        Name = id;
        Version = version;
    }
    [JsonPropertyName("name")] public string Name { get; }
    [JsonPropertyName("version")] public string Version { get; }
}

public class ClientCapabilities {
    [JsonPropertyName("testing")] public ClientTestingCapabilities Testing { get; set; }
    // I do not know what it is, because 'client/attachDebugger' event
    // does not fire if this property is set to true
    public ClientCapabilities(bool debuggerProvider) {
        Testing = new ClientTestingCapabilities() { DebuggerProvider = debuggerProvider };
    }
};
public class ClientTestingCapabilities {
    [JsonPropertyName("debuggerProvider")] public bool DebuggerProvider { get; set; }
}
