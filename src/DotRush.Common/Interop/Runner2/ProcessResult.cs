// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.
// dotnet/sdk/src/BuiltInTools/dotnet-format/Utilities/ProcessRunner.cs

using System.Collections.ObjectModel;
using System.Diagnostics;

namespace DotRush.Common.InteropV2;

public readonly struct ProcessResult {
    public Process Process { get; }
    public int ExitCode { get; }
    public ReadOnlyCollection<string> OutputLines { get; }
    public ReadOnlyCollection<string> ErrorLines { get; }
    public bool Success => ExitCode == 0;

    public ProcessResult(Process process, int exitCode, ReadOnlyCollection<string> outputLines, ReadOnlyCollection<string> errorLines) {
        Process = process;
        ExitCode = exitCode;
        OutputLines = outputLines;
        ErrorLines = errorLines;
    }

    public string GetError() => string.Join(Environment.NewLine, ErrorLines);
    public string GetOutput() => string.Join(Environment.NewLine, OutputLines);
}