namespace DotRush.Common.Interop.Apple;

public static class XCRun {
    public static void ShutdownAll(IProcessLogger? logger = null) {
        FileInfo tool = AppleSdkLocator.XCRunTool();
        ProcessResult result = new ProcessRunner(tool, new ProcessArgumentBuilder()
            .Append("simctl")
            .Append("shutdown")
            .Append("all"), logger)
            .WaitForExit();

        var output = string.Join(Environment.NewLine, result.StandardOutput) + Environment.NewLine;

        if (!result.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.StandardError));
    }
    public static void LaunchSimulator(string serial, IProcessLogger? logger = null) {
        var tool = AppleSdkLocator.OpenTool();
        ProcessResult result = new ProcessRunner(tool, new ProcessArgumentBuilder()
            .Append("-a", "Simulator")
            .Append("--args", "-CurrentDeviceUDID", serial), logger)
            .WaitForExit();

        var output = string.Join(Environment.NewLine, result.StandardOutput) + Environment.NewLine;

        if (!result.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.StandardError));
    }
}