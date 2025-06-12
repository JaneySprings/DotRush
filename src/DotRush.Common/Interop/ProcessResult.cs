namespace DotRush.Common.Interop;

public class ProcessResult {
    public List<string> StandardOutput { get; }
    public List<string> StandardError { get; }
    public int ExitCode { get; }

    public bool Success => this.ExitCode == 0;

    public string GetAllOutput() {
        return string.Join(Environment.NewLine, this.StandardOutput.Concat(this.StandardError));
    }

    public string GetOutput() {
        return string.Join(Environment.NewLine, this.StandardOutput);
    }
    public string GetError() {
        return string.Join(Environment.NewLine, this.StandardError);
    }

    internal ProcessResult(List<string> stdOut, List<string> stdErr, int exitCode) {
        this.StandardOutput = stdOut;
        this.StandardError = stdErr;
        this.ExitCode = exitCode;
    }
}