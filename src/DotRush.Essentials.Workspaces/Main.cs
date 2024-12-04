using System.Text.Json;
using DotRush.Essentials.Common.Logging;
using DotRush.Essentials.Common.MSBuild;
using DotRush.Essentials.Workspaces.Debugger;
using DotRush.Essentials.Workspaces.Models;

namespace DotRush.Essentials.Workspaces;

public class Program {
    public static readonly Dictionary<string, Action<string[]>> CommandHandler = new() {
        { "--list-proc", ListProcesses },
        { "--install-vsdbg", InstallDebugger },
        { "--project", GetProject }
    };

    private static void Main(string[] args) {
        if (args.Length == 0)
            return;
        if (CommandHandler.TryGetValue(args[0], out var command))
            command.Invoke(args);
    }

    public static void InstallDebugger(string[] args) {
        void SetResult(Status status) {
            Console.WriteLine(JsonSerializer.Serialize(status));
            Environment.Exit(0);
        }

        try {
            var url = VsdbgDownloader.ObtainDebuggerLinkAsync().Result;
            if (string.IsNullOrEmpty(url))
                SetResult(Status.Fail("Cannot optain debugger download link"));

            var executable = VsdbgDownloader.InstallDebuggerAsync(url!).Result;
            if (string.IsNullOrEmpty(executable))
                SetResult(Status.Fail("Cannot locate debugger executable"));

            SetResult(Status.Success());
        } catch (Exception ex) { 
            SetResult((Status.Fail(ex.Message))); 
        }
    }
    public static void ListProcesses(string[] args) {
        var processes = ProcessInfoProvider.GetProcesses();
        Console.WriteLine(JsonSerializer.Serialize(processes));
    }
    public static void GetProject(string[] args) {
        var project = MSBuildProjectsLoader.LoadProject(args[1], CurrentSessionLogger.Debug);
        Console.WriteLine(JsonSerializer.Serialize(project));
    }
}
