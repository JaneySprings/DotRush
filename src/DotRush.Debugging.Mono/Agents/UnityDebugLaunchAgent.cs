using System.Globalization;
using System.Net;
using System.Text.Json;
using DotRush.Common.Extensions;
using DotRush.Common.Interop;
using DotRush.Common.Interop.Android;
using DotRush.Common.MSBuild;
using DotRush.Debugging.Mono.Extensions;
using DotRush.Debugging.Mono.Models;
using Mono.Debugging.Soft;

namespace DotRush.Debugging.Mono;

public class UnityDebugLaunchAgent : BaseLaunchAgent {
    private readonly ExternalTypeResolver typeResolver;
    private SoftDebuggerStartInfo? startInformation;

    public UnityDebugLaunchAgent(LaunchConfiguration configuration) : base(configuration) {
        typeResolver = new ExternalTypeResolver(configuration.TransportId);
    }

    public override void Prepare(DebugSession debugSession) {
        var editorInstance = GetEditorInstance();

        if (Configuration.TransportArguments == null || Configuration.TransportArguments.Type == TransportType.Generic)
            startInformation = AttachToUnityPlayer(debugSession, editorInstance);
        if (Configuration.TransportArguments != null && Configuration.TransportArguments.Type == TransportType.Android)
            startInformation = AttachToAndroidPlayer(debugSession, editorInstance);

        ArgumentNullException.ThrowIfNull(startInformation, "Current transport configuration is not supported");
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

        var projectAssemblyPath = Path.Combine(Configuration.CurrentDirectory, "Library", "ScriptAssemblies", "Assembly-CSharp.dll");
        if (File.Exists(projectAssemblyPath))
            return new[] { projectAssemblyPath };

        var projectFilePaths = Directory.GetFiles(Configuration.CurrentDirectory, "*.csproj", SearchOption.TopDirectoryOnly);
        if (projectFilePaths.Length == 1) {
            var project = MSBuildProjectsLoader.LoadProject(projectFilePaths[0]);
            var assemblyName = project?.GetAssemblyName();
            projectAssemblyPath = Path.Combine(Configuration.CurrentDirectory, "Library", "ScriptAssemblies", $"{assemblyName}.dll");
            if (File.Exists(projectAssemblyPath))
                return new[] { projectAssemblyPath };
        }

        logger?.OnErrorDataReceived($"Could not find user assembly '{projectAssemblyPath}'. Specify 'userAssemblies' property in the launch configuration to override this behavior.");
        return Enumerable.Empty<string>();
    }

    private SoftDebuggerStartInfo AttachToUnityPlayer(DebugSession debugSession, EditorInstance editorInstance) {
        debugSession.OnOutputDataReceived($"Attaching to Unity({editorInstance.ProcessId}) - {editorInstance.Version}");

        var applicationName = Path.GetFileName(Configuration.CurrentDirectory);
        var port = Configuration.ProcessId != 0 ? Configuration.ProcessId : 56000 + (editorInstance.ProcessId % 1000);
        return new SoftDebuggerStartInfo(new SoftDebuggerConnectArgs(applicationName, IPAddress.Loopback, port));
    }
    private SoftDebuggerStartInfo AttachToAndroidPlayer(DebugSession debugSession, EditorInstance editorInstance) {
        debugSession.OnOutputDataReceived($"Attaching to Android process - Unity({editorInstance.Version})");
        ArgumentNullException.ThrowIfNull(Configuration.TransportArguments);
        AndroidSdkLocator.UnityEditorPath = editorInstance.AppPath;

        var serial = Configuration.TransportArguments.Serial;
        if (string.IsNullOrEmpty(serial)) {
            var devices = AndroidDebugBridge.Devices();
            if (devices.Count == 0)
                throw new InvalidOperationException("No Android devices found.");

            serial = devices[0];
        }

        var port = Configuration.TransportArguments.Port != 0 ? Configuration.TransportArguments.Port : GetAndroidPlayerPort(serial);
        ArgumentOutOfRangeException.ThrowIfZero(port, $"Failed to determine port for '{serial}'.");
        debugSession.OnOutputDataReceived($"Connecting to '{serial}' at port '{port}'");

        AndroidDebugBridge.Forward(serial, port);
        var logcatProcess = AndroidDebugBridge.Logcat(serial, "system,crash", "*:I", debugSession);
        Disposables.Add(() => AndroidDebugBridge.RemoveForward(serial));
        Disposables.Add(() => logcatProcess.Terminate());

        var applicationName = Path.GetFileName(Configuration.CurrentDirectory);
        return new SoftDebuggerStartInfo(new SoftDebuggerConnectArgs(applicationName, IPAddress.Loopback, port));
    }

    private EditorInstance GetEditorInstance() {
        var editorInfo = Path.Combine(Configuration.CurrentDirectory, "Library", "EditorInstance.json");
        if (!File.Exists(editorInfo))
            throw ServerExtensions.GetProtocolException($"EditorInstance.json not found: '{editorInfo}'. Is your Unity Editor running?");

        var editorInstance = SafeExtensions.Invoke(() => JsonSerializer.Deserialize<EditorInstance>(File.ReadAllText(editorInfo)));
        if (editorInstance == null)
            throw ServerExtensions.GetProtocolException($"Failed to deserialize EditorInstance.json: '{editorInfo}'");

        return editorInstance;
    }
    private int GetAndroidPlayerPort(string serial) {
        var debuggerLogs = AndroidDebugBridge.Logcat(serial, "managed debugger on port");
        if (debuggerLogs.Count == 0)
            return 0;

        var portLine = debuggerLogs.Last();
        var portString = portLine.Substring(portLine.LastIndexOf("port") + 5).Trim();
        return int.Parse(portString, CultureInfo.InvariantCulture);
    }
}