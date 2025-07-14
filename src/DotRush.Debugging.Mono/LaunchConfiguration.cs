using DotRush.Common.Extensions;
using DotRush.Debugging.Mono.Extensions;
using Mono.Debugging.Client;
using Newtonsoft.Json.Linq;

namespace DotRush.Debugging.Mono;

public class LaunchConfiguration {
    public int ProcessId { get; init; }
    public string? ProgramPath { get; init; }
    public string CurrentDirectory { get; init; }
    public Dictionary<string, string>? EnvironmentVariables { get; init; }
    public DebuggerSessionOptions DebuggerSessionOptions { get; init; }
    public List<string>? UserAssemblies { get; init; }

    public string? TransportId { get; init; }
    public TransportConfiguration? TransportArguments { get; init; }

    public LaunchConfiguration(Dictionary<string, JToken> configurationProperties) {
        ProcessId = configurationProperties.TryGetValue("processId").ToValue<int>();
        ProgramPath = configurationProperties.TryGetValue("program").ToClass<string>()?.ToPlatformPath();
        CurrentDirectory = configurationProperties.TryGetValue("cwd").ToClass<string>()?.ToPlatformPath().TrimPathEnd() ?? Environment.CurrentDirectory;
        EnvironmentVariables = configurationProperties.TryGetValue("env")?.ToClass<Dictionary<string, string>>();
        if (Directory.Exists(CurrentDirectory) && CurrentDirectory != Environment.CurrentDirectory)
            Environment.CurrentDirectory = CurrentDirectory;

        DebuggerSessionOptions = configurationProperties.TryGetValue("debuggerOptions")?.ToClass<DebuggerSessionOptions>() ?? ServerExtensions.DefaultDebuggerOptions;
        UserAssemblies = configurationProperties.TryGetValue("userAssemblies")?.ToClass<List<string>>();
        TransportId = configurationProperties.TryGetValue("transportId").ToClass<string>();
        TransportArguments = configurationProperties.TryGetValue("transportArgs")?.ToClass<TransportConfiguration>();
    }

    public BaseLaunchAgent GetLaunchAgent() {
        //TODO: Other launch agents can be added here in the future
        return new UnityDebugLaunchAgent(this);
    }
}

[Serializable]
public class TransportConfiguration {
    public TransportType Type { get; set; }
    public int Port { get; set; }
    public string? Serial { get; set; }
}

public enum TransportType {
    Generic,
    Android
}