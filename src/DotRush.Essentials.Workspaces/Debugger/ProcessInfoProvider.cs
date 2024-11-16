using System.Collections.Immutable;
using System.Diagnostics;
using DotRush.Essentials.Workspaces.Models;

namespace DotRush.Essentials.Workspaces.Debugger;

public static class ProcessInfoProvider {
    public static IEnumerable<ProcessInfo> GetProcesses() {
        return Process.GetProcesses().Select(it => new ProcessInfo(it)).ToImmutableArray();
    }
}