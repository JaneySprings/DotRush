using DotRush.Debugging.Unity.Extensions;
using Mono.Debugging.Soft;
using System.Net;

namespace DotRush.Debugging.Unity;

public class DebugLaunchAgent : BaseLaunchAgent {
    private readonly ExternalTypeResolver typeResolver;
    private SoftDebuggerStartInfo? startInformation;

    public DebugLaunchAgent(LaunchConfiguration configuration) : base(configuration) {
        typeResolver = new ExternalTypeResolver(configuration.TransportId);
    }
    public override void Attach(DebugSession debugSession) {
        var editorInstance = Configuration.GetEditorInstance();
        debugSession.OnOutputDataReceived($"Attaching to Unity({editorInstance.ProcessId}) - {editorInstance.Version}");

        var port = Configuration.ProcessId != 0 ? Configuration.ProcessId : 56000 + (editorInstance.ProcessId % 1000);
        startInformation = new SoftDebuggerStartInfo(new SoftDebuggerConnectArgs(Configuration.GetProjectName(), IPAddress.Loopback, port));
        startInformation.SetAssemblies(Configuration.GetAssembliesPath(), Configuration.DebuggerSessionOptions);
    }
    public override void Connect(SoftDebuggerSession session) {
        session.Run(startInformation, Configuration.DebuggerSessionOptions);
        if (typeResolver.TryConnect()) {
            Disposables.Add(() => typeResolver.Dispose());
            session.TypeResolverHandler = typeResolver.Resolve;
        }
    }

    // public static UnityAttachInfo GetUnityAttachInfo(long processId, ref IUnityDbgConnector connector) {
    //     if (ConnectorRegistry.Connectors.ContainsKey((uint)processId)) {
    //         connector = ConnectorRegistry.Connectors[(uint)processId];
    //         return connector.SetupConnection();
    //     } else if (UnityPlayers.ContainsKey((uint)processId)) {
    //         PlayerConnection.PlayerInfo player = UnityPlayers[(uint)processId];
    //         int port = 0 == player.m_DebuggerPort
    //             ? (int)(56000 + processId % 1000)
    //             : (int)player.m_DebuggerPort;
    //         try {
    //             return new UnityAttachInfo(player.m_Id, player.m_IPEndPoint.Address, port);
    //         } catch (Exception ex) {
    //             throw new Exception($"Unable to attach to {player.m_IPEndPoint.Address}:{port}", ex);
    //         }
    //     }

    //     long defaultPort = 56000 + (processId % 1000);
    //     return new UnityAttachInfo(null, IPAddress.Loopback, (int)defaultPort);
    // }
}