using Mono.Debugging.Soft;

namespace DotRush.Debugging.Unity;

public class NoDebugLaunchAgent : BaseLaunchAgent {
    public NoDebugLaunchAgent(LaunchConfiguration configuration) : base(configuration) { }
    public override void Attach(DebugSession debugSession) {
        throw new NotSupportedException();
    }
    public override void Connect(SoftDebuggerSession session) {}
}