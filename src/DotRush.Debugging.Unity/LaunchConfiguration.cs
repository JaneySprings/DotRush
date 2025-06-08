using DotRush.Common.Extensions;
using DotRush.Debugging.Unity.Extensions;
using Mono.Debugging.Client;
using Newtonsoft.Json.Linq;

namespace DotRush.Debugging.Unity;

public class LaunchConfiguration {
    public string WorkingDirectory { get; init; }
    public int ProcessId { get; init; }
    public string? TransportId { get; init; }
    public DebuggerSessionOptions DebuggerSessionOptions { get; init; }
    public Dictionary<string, string>? EnvironmentVariables { get; init; }
    public List<string>? UserAssemblies { get; init; }
    private bool SkipDebug { get; init; }

    public LaunchConfiguration(Dictionary<string, JToken> configurationProperties) {
        SkipDebug = configurationProperties.TryGetValue("skipDebug").ToValue<bool>();
        ProcessId = configurationProperties.TryGetValue("processId").ToValue<int>();
        TransportId = configurationProperties.TryGetValue("transportId").ToClass<string>();
        DebuggerSessionOptions = configurationProperties.TryGetValue("debuggerOptions")?.ToClass<DebuggerSessionOptions>() ?? ServerExtensions.DefaultDebuggerOptions;
        EnvironmentVariables = configurationProperties.TryGetValue("env")?.ToClass<Dictionary<string, string>>();
        UserAssemblies = configurationProperties.TryGetValue("userAssemblies")?.ToClass<List<string>>();

        var workingDirectory = configurationProperties.TryGetValue("cwd").ToClass<string>()?.ToPlatformPath().TrimPathEnd();
        WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
    }

    public BaseLaunchAgent GetLaunchAgent() {
        return new UnityDebugLaunchAgent(this); //NoDebug?
    }
}