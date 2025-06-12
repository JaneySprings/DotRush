using DotRush.Common;
using DotRush.Common.Extensions;
using DotRush.Common.InteropV2;
using DotRush.Debugging.NetCore.Installers;
using NUnit.Framework;

namespace DotRush.Debugging.NetCore.Tests;

public class DebuggerInstallerTests : TestFixture {
    private readonly string workingDirectory = AppDomain.CurrentDomain.BaseDirectory;
    private IDebuggerInstaller? debuggerInstaller;

    [Test]
    public void InstallDebuggerVsdbgTest() {
        debuggerInstaller = new VsdbgInstaller(workingDirectory);

        debuggerInstaller.BeginInstallation();
        Assert.That(Directory.Exists(Path.Combine(workingDirectory, "Debugger")), Is.False, "Debugger directory not deleted");

        var downloadLink = debuggerInstaller.GetDownloadLink();
        Assert.That(downloadLink, Is.Not.Null.Or.Empty);

        var executable = debuggerInstaller.Install(downloadLink!);
        Assert.That(executable, Is.Not.Null.Or.Empty);
        Assert.That(File.Exists(executable), Is.True, $"Debugger executable not found at '{executable}'");
        Assert.That(Path.GetFileName(executable), Is.EqualTo("vsdbg-ui" + RuntimeInfo.ExecExtension));
        Assert.That(PathExtensions.Equals(executable, Path.Combine(workingDirectory, "Debugger", "vsdbg-ui" + RuntimeInfo.ExecExtension)), Is.True, "Debugger executable not in Debugger directory");

        debuggerInstaller.EndInstallation(executable!);
        Assert.That(File.Exists(Path.Combine(Path.GetDirectoryName(executable)!, "clrdbg" + RuntimeInfo.ExecExtension)), Is.True, $"Link to clrdbg not found in '{executable}'");
        if (!RuntimeInfo.IsWindows) {
            var result = ProcessRunner.CreateProcess("ls", $"-l {executable}", captureOutput: true, displayWindow: false).Task.Result;
            Assert.That(result.Success, Is.True, $"Failed to check +x flag: {result.GetError()}");
            Assert.That(result.GetOutput(), Does.StartWith("-rwx"));
        }
    }

    [Test]
    public void InstallDebuggerNcdbgTest() {
        debuggerInstaller = new NcdbgInstaller(workingDirectory);

        debuggerInstaller.BeginInstallation();
        Assert.That(Directory.Exists(Path.Combine(workingDirectory, "Debugger")), Is.False, "Debugger directory not deleted");

        var downloadLink = debuggerInstaller.GetDownloadLink();
        Assert.That(downloadLink, Is.Not.Null.Or.Empty);

        var executable = debuggerInstaller.Install(downloadLink!);
        Assert.That(executable, Is.Not.Null.Or.Empty);
        Assert.That(File.Exists(executable), Is.True, $"Debugger executable not found at '{executable}'");
        Assert.That(Path.GetFileName(executable), Is.EqualTo("netcoredbg" + RuntimeInfo.ExecExtension));
        Assert.That(PathExtensions.Equals(executable, Path.Combine(workingDirectory, "Debugger", "netcoredbg" + RuntimeInfo.ExecExtension)), Is.True, "Debugger executable not in Debugger directory");

        debuggerInstaller.EndInstallation(executable!);
        Assert.That(File.Exists(Path.Combine(Path.GetDirectoryName(executable)!, "clrdbg" + RuntimeInfo.ExecExtension)), Is.True, $"Link to clrdbg not found in '{executable}'");
        if (!RuntimeInfo.IsWindows) {
            var result = ProcessRunner.CreateProcess("ls", $"-l {executable}", captureOutput: true, displayWindow: false).Task.Result;
            Assert.That(result.Success, Is.True, $"Failed to check +x flag: {result.GetError()}");
            Assert.That(result.GetOutput(), Does.StartWith("-rwx"));
        }
    }
}