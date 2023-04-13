using System.Diagnostics;

namespace DotRush.Server.Processes;

public class ProcessRunner {
    private List<string> standardOutput = new List<string>();
    private List<string> standardError = new List<string>();
    private readonly Process process;


    public ProcessRunner(string command, ProcessArgumentBuilder? builder = null) {
        this.process = new Process();
        this.process.StartInfo.Arguments = builder?.ToString();
        this.process.StartInfo.FileName = command;

        SetupProcessLogging(null);
    }

    private void SetupProcessLogging(IProcessLogger? logger = null) {
        this.process.StartInfo.CreateNoWindow = true;
        this.process.StartInfo.UseShellExecute = false;
        this.process.StartInfo.RedirectStandardOutput = true;
        this.process.StartInfo.RedirectStandardError = true;
        this.process.StartInfo.RedirectStandardInput = true;
        this.process.OutputDataReceived += (s, e) => {
            if (e.Data != null) {
                if (logger != null)
                    logger.OnOutputDataReceived(e.Data);
                else this.standardOutput.Add(e.Data);
            }
        };
        this.process.ErrorDataReceived += (s, e) => {
            if (e.Data != null) {
                if (logger != null)
                    logger.OnErrorDataReceived(e.Data);
                else this.standardError.Add(e.Data);
            }
        };
    }

    public ProcessResult WaitForExit() {
        this.process.Start();
        this.process.BeginOutputReadLine();
        this.process.BeginErrorReadLine();
        this.process.WaitForExit();
        return new ProcessResult(this.standardOutput, this.standardError, this.process.ExitCode);
    }
}