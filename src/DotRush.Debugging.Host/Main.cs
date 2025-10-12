using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using DotRush.Common.Extensions;
using DotRush.Common.MSBuild;
using DotRush.Debugging.Host.Extensions;
using DotRush.Debugging.Host.Installers;
using DotRush.Debugging.Host.TemplateEngine;
using DotRush.Debugging.Host.TestPlatform;

namespace DotRush.Debugging.Host;

public class Program {
    public static int Main(string[] args) {
        var installVsdbgOption = new Option<bool>("--install-vsdbg", "-vsdbg");
        var installNcdbgOption = new Option<bool>("--install-ncdbg", "-ncdbg");
        var evaluateProjectOption = new Option<string>("--project", "-p");
        var processListOption = new Option<bool>("--processes", "-ps");
        var rootCommand = new RootCommand("DotRush Test Host") {
            Options = {
                installVsdbgOption,
                installNcdbgOption,
                evaluateProjectOption,
                processListOption
            },
            Subcommands = {
                CreateTestCommand(),
                CreateNewCommand()
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
            if (result.GetValue(processListOption)) {
                var processes = Process.GetProcesses().Select(it => new ProcessInfo(it));
                Console.WriteLine(JsonSerializer.Serialize(processes));
                return;
            }
            if (result.GetValue(evaluateProjectOption) is string projectPath) {
                var project = MSBuildProjectsLoader.LoadProject(args[1]);
                Console.WriteLine(JsonSerializer.Serialize(project));
                return;
            }
        });

        return rootCommand.Parse(args).Invoke();
    }

    private static Command CreateTestCommand() {
        var attachDebuggerOption = new Option<bool>("--debug", "-d");
        var testAssembliesOption = new Option<string[]>("--assemblies", "-a");
        var testCaseFilterOption = new Option<string[]>("--filter", "-f");
        var runSettingsOption = new Option<string>("--settings", "-s");
        var useTestingPlatformOption = new Option<bool>("--mtp", "-m");
        var testCommand = new Command("test") {
            Options = {
                attachDebuggerOption,
                testAssembliesOption,
                testCaseFilterOption,
                runSettingsOption
            }
        };
        testCommand.SetAction(result => {
            var attachDebugger = result.GetValue(attachDebuggerOption);
            ITestHostAdapter testHostAdapter = result.GetValue(useTestingPlatformOption)
                ? new TestingPlatformHostAdapter(attachDebugger)
                : new VSTestHostAdapter(attachDebugger);

            return testHostAdapter.StartSession(
                result.GetTrimmedValue(testAssembliesOption),
                result.GetTrimmedValue(testCaseFilterOption),
                result.GetTrimmedValue(runSettingsOption)
            );
        });
        return testCommand;
    }
    private static Command CreateNewCommand() {
        var templateListOption = new Option<bool>("--list", "-l");
        var templateIdentityOption = new Option<string>("--identity", "-i");
        var templateOutputOption = new Option<string>("--output", "-o");
        var templateParametersOption = new Option<string>("--parameters", "-p");
        var newCommand = new Command("new") {
            Options = {
                templateListOption,
                templateIdentityOption,
                templateOutputOption,
                templateParametersOption
            }
        };
        newCommand.SetAction(result => {
            var templateHostAdapter = new TemplateHostAdapter();
            if (result.GetValue(templateListOption)) {
                var templates = templateHostAdapter.GetTemplatesAsync(CancellationToken.None).Result;
                var projectTemplates = templates.Select(t => new ProjectTemplate(t));
                Console.WriteLine(JsonSerializer.Serialize(projectTemplates));
                return;
            }

            var identity = result.GetTrimmedValue(templateIdentityOption);
            var output = result.GetTrimmedValue(templateOutputOption);
            if (string.IsNullOrEmpty(identity) || string.IsNullOrEmpty(output)) {
                Console.WriteLine(JsonSerializer.Serialize(Status.Fail("Template identity and output path are required")));
                return;
            }

            var parameters = new Dictionary<string, string?>();
            var parameterString = result.GetTrimmedValue(templateParametersOption);
            if (!string.IsNullOrEmpty(parameterString))
                parameters = SafeExtensions.Invoke(() => JsonSerializer.Deserialize<Dictionary<string, string?>>(parameterString));

            var status = templateHostAdapter.CreateTemplateAsync(identity, output, parameters, CancellationToken.None).Result;
            if (status == null)
                Console.WriteLine(JsonSerializer.Serialize(Status.Fail("Operation cancelled")));
            else
                Console.WriteLine(JsonSerializer.Serialize(status));
        });
        return newCommand;
    }

    private static void InstallDebugger(IDebuggerInstaller installer) {
        void SetResult(Status status, int exitCode = 0) {
            Console.WriteLine(JsonSerializer.Serialize(status));
            Environment.Exit(exitCode);
        }

        try {
            installer.BeginInstallation();
            var url = installer.GetDownloadLink();
            if (string.IsNullOrEmpty(url))
                SetResult(Status.Fail("Cannot optain debugger download link"), 404);

            var executable = installer.Install(url!);
            if (string.IsNullOrEmpty(executable))
                SetResult(Status.Fail("Cannot locate debugger executable"), 500);

            installer.EndInstallation(executable!);
            SetResult(Status.Success());
        } catch (Exception ex) {
            SetResult((Status.Fail(ex.Message)));
        }
    }
}
