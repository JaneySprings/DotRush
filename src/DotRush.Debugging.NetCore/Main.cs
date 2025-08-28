using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using DotRush.Common.Extensions;
using DotRush.Common.MSBuild;
using DotRush.Debugging.NetCore.Installers;
using DotRush.Debugging.NetCore.Models;
using DotRush.Debugging.NetCore.TestPlatform;

namespace DotRush.Debugging.NetCore;

public class Program {
    public static int Main(string[] args) {
        var attachDebuggerOption = new Option<bool>("--debug", "-d");
        var testAssembliesOption = new Option<string[]>("--assemblies", "-a");
        var testCaseFilterOption = new Option<string[]>("--filter", "-f");
        var runSettingsOption = new Option<string>("--settings", "-s");
        // Helpers
        var installVsdbgOption = new Option<bool>("--install-vsdbg", "-vsdbg");
        var installNcdbgOption = new Option<bool>("--install-ncdbg", "-ncdbg");
        var evaluateProjectOption = new Option<string>("--project", "-p");
        var listProcessesOption = new Option<bool>("--processes", "-ps");

        var rootCommand = new RootCommand("DotRush Test Host") {
            Options = {
                attachDebuggerOption,
                testAssembliesOption,
                testCaseFilterOption,
                runSettingsOption,
                installVsdbgOption,
                installNcdbgOption,
                evaluateProjectOption,
                listProcessesOption
            }
        };
        rootCommand.SetAction(result => {
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

            var testHostAdapter = new TestHostAdapter(result.GetValue(attachDebuggerOption));
            testHostAdapter.StartSession(
                result.GetTrimmedValue(testAssembliesOption),
                result.GetTrimmedValue(testCaseFilterOption),
                result.GetTrimmedValue(runSettingsOption)
            );
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

internal static class CommandLineExtensions {
    public static string? GetTrimmedValue(this ParseResult result, Option<string> option) {
        var rawValue = result.GetValue(option);
        if (!string.IsNullOrEmpty(rawValue))
            return TrimPath(rawValue);
        return rawValue;
    }
    public static string[] GetTrimmedValue(this ParseResult result, Option<string[]> option) {
        var rawValues = result.GetValue(option);
        if (rawValues != null && rawValues.Length > 0)
            return rawValues.Select(TrimPath).ToArray();
        return rawValues ?? Array.Empty<string>();
    }

    private static string TrimPath(string rawPath) {
        return rawPath.Trim('"', '\'').ToPlatformPath();
    }
}