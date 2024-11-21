using System.Text.RegularExpressions;
using DotRush.Essentials.Common.External;
using DotRush.Essentials.Common.Logging;

namespace DotRush.Essentials.TestExplorer;

public static class TestHost {
    private static string reportsDirectory;
    
    static TestHost() {
        reportsDirectory = AppDomain.CurrentDomain.BaseDirectory;
    }

    public static Task<int> RunForDebugAsync(string invocation) {
        CurrentSessionLogger.Debug($"Running test host with command line '{invocation}'");
        
        var tcs = new TaskCompletionSource<int>();
        var process = new ProcessRunner("dotnet", new ProcessArgumentBuilder()
            .Append("test")
            .Append(invocation), new HandleStartProcessLogger(tcs));

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