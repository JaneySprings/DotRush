using System.Diagnostics;
using Newtonsoft.Json;
using StreamJsonRpc;

namespace DotRush.Debugging.Host.Performance;

public class ProcessUsageSampler {
    public int StartSession(int processId) {
        if (processId <= 0)
            return 1;

        var rpcServer = JsonRpc.Attach(Console.OpenStandardOutput(), Console.OpenStandardInput());
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        Console.SetIn(TextReader.Null);

        var stopRequested = false;
        rpcServer.AllowModificationWhileListening = true;
        rpcServer.AddLocalRpcMethod("handleSamplingStop", () => stopRequested = true);

        TimeSpan? lastCpuTime = null;
        long lastTimestamp = 0;
        while (!stopRequested && !rpcServer.Completion.IsCompleted) {
            try {
                using var process = Process.GetProcessById(processId);
                var cpuTime = process.TotalProcessorTime;
                var timestamp = Stopwatch.GetTimestamp();
                double? cpuUsage = null;
                if (lastCpuTime != null)
                    cpuUsage = Math.Clamp((cpuTime - lastCpuTime.Value) / Stopwatch.GetElapsedTime(lastTimestamp, timestamp) / Environment.ProcessorCount * 100, 0, 100);
                lastCpuTime = cpuTime;
                lastTimestamp = timestamp;

                rpcServer.NotifyAsync("handleUsageSample", new ProcessUsage {
                    WorkingSet = process.WorkingSet64,
                    // // Not reported on macOS, where it always reads as zero
                    // PrivateBytes = process.PrivateMemorySize64 > 0 ? process.PrivateMemorySize64 : null,
                    CpuUsage = cpuUsage,
                }).Wait();
            }
            catch {
                break;
            }
            Thread.Sleep(1000);
        }
        return 0;
    }
}

public class ProcessUsage {
    [JsonProperty("workingSet")]
    public long WorkingSet { get; set; }

    [JsonProperty("privateBytes", NullValueHandling = NullValueHandling.Ignore)]
    public long? PrivateBytes { get; set; }

    [JsonProperty("cpuUsage", NullValueHandling = NullValueHandling.Ignore)]
    public double? CpuUsage { get; set; }
}
