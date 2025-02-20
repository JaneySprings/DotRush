using DotRush.Debugging.Unity.Extensions;
using Newtonsoft.Json.Linq;
using Mono.Debugging.Client;
using System.Text.Json;
using DotRush.Common.Extensions;
using DotRush.Debugging.Unity.Models;

namespace DotRush.Debugging.Unity;

public class LaunchConfiguration {
    public string WorkingDirectory { get; init; }
    public int ProcessId { get; init; }
    public string? TransportId { get; init; }
    public Dictionary<string, string> EnvironmentVariables { get; init; }
    public DebuggerSessionOptions DebuggerSessionOptions { get; init; }

    private bool SkipDebug { get; init; }

    public LaunchConfiguration(Dictionary<string, JToken> configurationProperties) {
        SkipDebug = configurationProperties.TryGetValue("skipDebug").ToValue<bool>();
        ProcessId = configurationProperties.TryGetValue("processId").ToValue<int>();
        TransportId = configurationProperties.TryGetValue("transportId").ToClass<string>();
        DebuggerSessionOptions = configurationProperties.TryGetValue("debuggerOptions")?.ToClass<DebuggerSessionOptions>() 
            ?? ServerExtensions.DefaultDebuggerOptions;
        EnvironmentVariables = configurationProperties.TryGetValue("env")?.ToClass<Dictionary<string, string>>()
            ?? new Dictionary<string, string>();

        var workingDirectory = configurationProperties.TryGetValue("cwd").ToClass<string>()?.ToPlatformPath().TrimPathEnd();
        WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
    }

    public string GetAssembliesPath() {
        return Path.Combine(WorkingDirectory, "Library", "ScriptAssemblies");
    }
    public string GetProjectName() {
        return Path.GetFileName(WorkingDirectory);
    }
    public BaseLaunchAgent GetLaunchAgent() {
        return new DebugLaunchAgent(this); //NoDebug?
    }
    public EditorInstance GetEditorInstance() {
        var editorInfo = Path.Combine(WorkingDirectory, "Library", "EditorInstance.json");
        if (!File.Exists(editorInfo))
            throw ServerExtensions.GetProtocolException($"EditorInstance.json not found: '{editorInfo}'");

        var editorInstance = JsonSerializer.Deserialize<EditorInstance>(File.ReadAllText(editorInfo));
        if (editorInstance == null)
            throw ServerExtensions.GetProtocolException($"Failed to deserialize EditorInstance.json: '{editorInfo}'");

        return editorInstance;
    }
}