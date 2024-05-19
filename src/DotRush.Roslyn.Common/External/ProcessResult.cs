// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.
// dotnet/sdk/src/BuiltInTools/dotnet-format/Utilities/ProcessRunner.cs

using System.Collections.ObjectModel;
using System.Diagnostics;

namespace DotRush.Roslyn.Common.External;

public readonly struct ProcessResult {
    public Process Process { get; }
    public int ExitCode { get; }
    public ReadOnlyCollection<string> OutputLines { get; }
    public ReadOnlyCollection<string> ErrorLines { get; }

    public ProcessResult(Process process, int exitCode, ReadOnlyCollection<string> outputLines, ReadOnlyCollection<string> errorLines) {
        Process = process;
        ExitCode = exitCode;
        OutputLines = outputLines;
        ErrorLines = errorLines;
    }
}