using System.Diagnostics;
using System.Text.Json;
using DotRush.Common.Logging;
using DotRush.Common.MSBuild;
using DotRush.Debugging.NetCore.Models;
using DotRush.Debugging.NetCore.Testing;
using DotRush.Debugging.NetCore.Testing.Explorer;

namespace DotRush.Debugging.NetCore;

public class Program {
    public static readonly Dictionary<string, Action<string[]>> CommandHandler = new() {
        { "--list-proc", ListProcesses },
        { "--list-tests", DiscoverTests },
        { "--install-vsdbg", InstallDebugger },
        { "--convert", ConvertReport },
        { "--project", GetProject },
        { "--run", RunTestHost }
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
            var url = NetCoreDebugger.ObtainDebuggerLinkAsync().Result;
            if (string.IsNullOrEmpty(url))
                SetResult(Status.Fail("Cannot optain debugger download link"));

            var executable = NetCoreDebugger.InstallDebuggerAsync(url!).Result;
            if (string.IsNullOrEmpty(executable))
                SetResult(Status.Fail("Cannot locate debugger executable"));

            SetResult(Status.Success());
        } catch (Exception ex) { 
            SetResult((Status.Fail(ex.Message))); 
        }
    }
    public static void ListProcesses(string[] args) {
        var processes = Process.GetProcesses().Select(it => new ProcessInfo(it));
        Console.WriteLine(JsonSerializer.Serialize(processes));
    }
    public static void GetProject(string[] args) {
        var project = MSBuildProjectsLoader.LoadProject(args[1], CurrentSessionLogger.Debug);
        Console.WriteLine(JsonSerializer.Serialize(project));
    }
    public static void DiscoverTests(string[] args) {
        var tests = new TestExplorer().DiscoverTests(args[1]);
        Console.WriteLine(JsonSerializer.Serialize(tests));
    }
    public static void ConvertReport(string[] args) {
        var results = ReportConverter.ReadReport(args[1]);
        Console.WriteLine(JsonSerializer.Serialize(results));
    }
    public static void RunTestHost(string[] args) {
        var result = TestHost.RunForDebugAsync(args[1], args[2]).Result;
        Console.WriteLine(JsonSerializer.Serialize(result));
    }
}
