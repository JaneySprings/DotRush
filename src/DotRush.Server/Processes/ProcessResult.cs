namespace DotRush.Server.Processes;

public class ProcessResult {
    public readonly List<string> StandardOutput;
    public readonly List<string> StandardError;

    public readonly int ExitCode;

    public bool Success => this.ExitCode == 0;

    public string GetAllOutput() {
        return string.Join(Environment.NewLine, this.StandardOutput.Concat(this.StandardError));
    }

    public string GetOutput() {
        return string.Join(Environment.NewLine, this.StandardOutput);
    }

    internal ProcessResult(List<string> stdOut, List<string> stdErr, int exitCode) {
        this.StandardOutput = stdOut;
        this.StandardError = stdErr;
        this.ExitCode = exitCode;
    }
}