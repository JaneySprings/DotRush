using System.Text.RegularExpressions;
using DotRush.Common.Interop;
using DotRush.Common.Logging;

namespace DotRush.Debugging.NetCore.Testing;

public static class TestHost {
    public static Task<int> RunForDebugAsync(string projectFile, string filter) {
        CurrentSessionLogger.Debug($"Running test host with filter '{filter}'");

        var tcs = new TaskCompletionSource<int>();
        var process = new ProcessRunner("dotnet", new ProcessArgumentBuilder()
            .Append("test")
            .Append(projectFile)
            .Append("--no-build")
            .Conditional($"--filter {filter}", () => !string.IsNullOrEmpty(filter)),
            new HandleStartProcessLogger(tcs)
        );

        process.SetEnvironmentVariable("VSTEST_HOST_DEBUG", "1");
        process.Start();

        return tcs.Task;
    }

    private class HandleStartProcessLogger : IProcessLogger {
        private TaskCompletionSource<int> tcs;

        public HandleStartProcessLogger(TaskCompletionSource<int> tcs) {
            this.tcs = tcs;
        }

        void IProcessLogger.OnErrorDataReceived(string stderr) {
            CurrentSessionLogger.Error(stderr);
        }
        void IProcessLogger.OnOutputDataReceived(string stdout) {
            CurrentSessionLogger.Debug(stdout);
            if (!stdout.Contains("Process Id:", StringComparison.OrdinalIgnoreCase))
                return;

            var match = Regex.Match(stdout, @"Process Id: (\d+)");
            if (!match.Success)
                return;

            tcs.SetResult(int.Parse(match.Groups[1].Value));
        }
    }
}