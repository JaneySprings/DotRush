using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using DotRush.Common.MSBuild;
using DotRush.Debugging.NetCore.Installers;
using DotRush.Debugging.NetCore.Models;
using DotRush.Debugging.NetCore.TestPlatform;

namespace DotRush.Debugging.NetCore;

public class Program {
    public static int Main(string[] args) {
        var waitDebuggerOption = new Option<bool>("--debug", "-d");
        var testAssembliesOption = new Option<string[]>("--assemblies", "-a");
        var testCaseFilterOption = new Option<string[]>("--filter", "-f");
        // Helpers
        var installVsdbgOption = new Option<bool>("--install-vsdbg", "-vsdbg");
        var installNcdbgOption = new Option<bool>("--install-ncdbg", "-ncdbg");
        var evaluateProjectOption = new Option<string>("--project", "-p");
        var listProcessesOption = new Option<bool>("--processes", "-ps");

        var rootCommand = new RootCommand("DotRush Test Host") {
            Options = {
                waitDebuggerOption,
                testAssembliesOption,
                testCaseFilterOption,
                installVsdbgOption,
                installNcdbgOption,
                evaluateProjectOption,
                listProcessesOption
            }
        };
        rootCommand.SetAction(result => {
            if (result.GetValue(waitDebuggerOption)) {
                while (!Debugger.IsAttached)
                    Thread.Sleep(200);
            }

            if (result.GetValue(installVsdbgOption)) {
                var workingDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
                InstallDebugger(new VsdbgInstaller(workingDirectory));
                return;
            }
            if (result.GetValue(installNcdbgOption)) {
                var workingDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
                InstallDebugger(new NcdbgInstaller(workingDirectory));
                return;
            }
            if (result.GetValue(listProcessesOption)) {
                var processes = Process.GetProcesses().Select(it => new ProcessInfo(it));
                Console.WriteLine(JsonSerializer.Serialize(processes));
                return;
            }
            if (result.GetValue(evaluateProjectOption) is string projectPath) {
                var project = MSBuildProjectsLoader.LoadProject(args[1]);
                Console.WriteLine(JsonSerializer.Serialize(project));
                return;
            }

            var testAssemblies = result.GetValue(testAssembliesOption) ?? Array.Empty<string>();
            var testCaseFilter = result.GetValue(testCaseFilterOption) ?? Array.Empty<string>();
            var testHostAdapter = new TestHostAdapter(null);
            testHostAdapter.StartSession(testAssemblies, testCaseFilter);
        });

        return rootCommand.Parse(args).Invoke();
    }

    private static void InstallDebugger(IDebuggerInstaller installer) {
        void SetResult(Status status) {
            Console.WriteLine(JsonSerializer.Serialize(status));
            Environment.Exit(0);
        }

        try {
            installer.BeginInstallation();
            var url = installer.GetDownloadLink();
            if (string.IsNullOrEmpty(url))
                SetResult(Status.Fail("Cannot optain debugger download link"));

            var executable = installer.Install(url!);
            if (string.IsNullOrEmpty(executable))
                SetResult(Status.Fail("Cannot locate debugger executable"));

            installer.EndInstallation(executable!);
            SetResult(Status.Success());
        } catch (Exception ex) {
            SetResult((Status.Fail(ex.Message)));
        }
    }
}