using System.Net;
using System.Text.Json;
using DotRush.Common.Extensions;
using DotRush.Common.ExternalV2;
using DotRush.Common.MSBuild;
using DotRush.Debugging.Unity.Extensions;
using DotRush.Debugging.Unity.Models;
using Mono.Debugging.Soft;

namespace DotRush.Debugging.Unity;

public class UnityDebugLaunchAgent : BaseLaunchAgent {
    private readonly ExternalTypeResolver typeResolver;
    private SoftDebuggerStartInfo? startInformation;

    public UnityDebugLaunchAgent(LaunchConfiguration configuration) : base(configuration) {
        typeResolver = new ExternalTypeResolver(configuration.TransportId);
    }

    public override void Prepare(DebugSession debugSession) {
        var editorInstance = GetEditorInstance();
        debugSession.OnOutputDataReceived($"Attaching to Unity({editorInstance.ProcessId}) - {editorInstance.Version}");

        var port = Configuration.ProcessId != 0 ? Configuration.ProcessId : 56000 + (editorInstance.ProcessId % 1000);
        var applicationName = Path.GetFileName(Configuration.WorkingDirectory);
        startInformation = new SoftDebuggerStartInfo(new SoftDebuggerConnectArgs(applicationName, IPAddress.Loopback, port));
        SetAssemblies(startInformation, debugSession);
    }
    public override void Connect(SoftDebuggerSession session) {
        session.Run(startInformation, Configuration.DebuggerSessionOptions);
        if (typeResolver.TryConnect()) {
            Disposables.Add(() => typeResolver.Dispose());
            session.TypeResolverHandler = typeResolver.Resolve;
        }
    }
    public override IEnumerable<string> GetUserAssemblies(IProcessLogger? logger) {
        if (Configuration.UserAssemblies != null && Configuration.UserAssemblies.Count > 0)
            return Configuration.UserAssemblies;

        var projectAssemblyPath = Path.Combine(Configuration.WorkingDirectory, "Library", "ScriptAssemblies", "Assembly-CSharp.dll");
        if (File.Exists(projectAssemblyPath))
            return new[] { projectAssemblyPath };

        var projectFilePaths = Directory.GetFiles(Configuration.WorkingDirectory, "*.csproj", SearchOption.TopDirectoryOnly);
        if (projectFilePaths.Length == 1) {
            var project = MSBuildProjectsLoader.LoadProject(projectFilePaths[0]);
            var assemblyName = project?.GetAssemblyName();
            projectAssemblyPath = Path.Combine(Configuration.WorkingDirectory, "Library", "ScriptAssemblies", $"{assemblyName}.dll");
            if (File.Exists(projectAssemblyPath))
                return new[] { projectAssemblyPath };
        }

        logger?.OnErrorDataReceived($"Could not find user assembly '{projectAssemblyPath}'. Specify 'userAssemblies' property in the launch configuration to override this behavior.");
        return Enumerable.Empty<string>();
    }

    private EditorInstance GetEditorInstance() {
        var editorInfo = Path.Combine(Configuration.WorkingDirectory, "Library", "EditorInstance.json");
        if (!File.Exists(editorInfo))
            throw ServerExtensions.GetProtocolException($"EditorInstance.json not found: '{editorInfo}'");

        var editorInstance = SafeExtensions.Invoke(() => JsonSerializer.Deserialize<EditorInstance>(File.ReadAllText(editorInfo)));
        if (editorInstance == null)
            throw ServerExtensions.GetProtocolException($"Failed to deserialize EditorInstance.json: '{editorInfo}'");

        return editorInstance;
    }
}