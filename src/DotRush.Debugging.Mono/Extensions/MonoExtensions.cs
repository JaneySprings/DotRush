using System.Text;
using DotRush.Common.Extensions;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;

namespace DotRush.Debugging.Mono.Extensions;

public static class MonoExtensions {
    public static string ToThreadName(this string threadName, int threadId) {
        if (!string.IsNullOrEmpty(threadName))
            return threadName;
        if (threadId == 1)
            return "Main Thread";
        return $"Thread #{threadId}";
    }
    public static string ToDisplayValue(this ObjectValue value) {
        var dv = value.DisplayValue ?? "<error getting value>";
        if (dv.Length > 1 && dv[0] == '{' && dv[dv.Length - 1] == '}')
            dv = dv.Substring(1, dv.Length - 2).Replace(Environment.NewLine, " ");
        return dv;
    }

    public static StackFrame? GetFrameSafe(this Backtrace bt, int n) {
        try {
            return bt.GetFrame(n);
        } catch (Exception e) {
            DebuggerLoggingService.CustomLogger?.LogError($"Error while getting frame [{n}]", e);
            return null;
        }
    }
    public static string GetAssemblyCode(this StackFrame frame) {
        var assemblyLines = frame.Disassemble(-1, -1);
        var sb = new StringBuilder();
        foreach (var line in assemblyLines)
            sb.AppendLine($"({line.SourceLine}) IL_{line.Address:0000}: {line.Code}");

        return sb.ToString();
    }
    public static bool HasNullValue(this ObjectValue objectValue) {
        return objectValue.Value == "(null)";
    }
    public static string ResolveValue(this ObjectValue variable, string value) {
        var fullName = variable.TypeName;
        if (string.IsNullOrEmpty(fullName))
            return value;

        var shortName = fullName.Split('.').Last();
        if (!value.StartsWith($"new {shortName}"))
            return value;

        return value.Replace($"new {shortName}", $"new {fullName}");
    }
    public static ThreadInfo? FindThread(this SoftDebuggerSession session, long id) {
        var process = session.GetProcesses().FirstOrDefault();
        if (process == null)
            return null;

        return process.GetThreads().FirstOrDefault(it => it.Id == id);
    }
    public static ExceptionInfo? FindException(this SoftDebuggerSession session, long id) {
        var thread = session.FindThread(id);
        if (thread == null)
            return null;

        for (int i = 0; i < thread.Backtrace.FrameCount; i++) {
            var frame = thread.Backtrace.GetFrameSafe(i);
            var ex = frame?.GetException();
            if (ex != null)
                return ex;
        }

        return null;
    }
    public static string? RemapSourceLocation(this SoftDebuggerSession session, SourceLocation location) {
        if (string.IsNullOrEmpty(location.FileName))
            return null;

        foreach (var remap in session.Options.SourceCodeMappings) {
            var filePath = location.FileName.ToPlatformPath();
            var key = remap.Key.ToPlatformPath();
            var value = remap.Value.ToPlatformPath();
            if (filePath.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                return filePath.Replace(key, value);
        }

        return location.FileName;
    }
}