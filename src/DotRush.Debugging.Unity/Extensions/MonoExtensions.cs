using System.Reflection;
using System.Text;
using DotRush.Common.Extensions;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;

namespace DotRush.Debugging.Unity.Extensions;

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

    public static void SetAssemblies(this SoftDebuggerStartInfo startInfo, string assembliesDirectory, DebuggerSessionOptions options) {
        var useSymbolServers = options.SearchMicrosoftSymbolServer || options.SearchNuGetSymbolServer;
        var assemblyPaths = Directory.EnumerateFiles(assembliesDirectory, "*.dll");
        var assemblyPathMap = new Dictionary<string, string>();
        var assemblySymbolPathMap = new Dictionary<string, string>();
        var assemblyNames = new List<AssemblyName>();

        foreach (var assemblyPath in assemblyPaths) {
            try {
                var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                if (string.IsNullOrEmpty(assemblyName.FullName) || string.IsNullOrEmpty(assemblyName.Name)) {
                    DebuggerLoggingService.CustomLogger?.LogMessage($"Assembly '{assemblyPath}' has no name");
                    continue;
                }

                string? assemblySymbolsFilePath = SymbolServerExtensions.SearchSymbols(options.SymbolSearchPaths, assemblyPath);
                if (string.IsNullOrEmpty(assemblySymbolsFilePath) && options.SearchMicrosoftSymbolServer)
                    assemblySymbolsFilePath = SymbolServerExtensions.DownloadSourceSymbols(assemblyPath, assemblyName.Name, SymbolServerExtensions.MicrosoftSymbolServerAddress);
                if (string.IsNullOrEmpty(assemblySymbolsFilePath) && options.SearchNuGetSymbolServer)
                    assemblySymbolsFilePath = SymbolServerExtensions.DownloadSourceSymbols(assemblyPath, assemblyName.Name, SymbolServerExtensions.NuGetSymbolServerAddress);
                if (string.IsNullOrEmpty(assemblySymbolsFilePath))
                    DebuggerLoggingService.CustomLogger?.LogMessage($"No symbols found for '{assemblyPath}'");

                if (!string.IsNullOrEmpty(assemblySymbolsFilePath))
                    assemblySymbolPathMap.Add(assemblyName.FullName, assemblySymbolsFilePath);

                if (options.ProjectAssembliesOnly && SymbolServerExtensions.HasDebugSymbols(assemblyPath, useSymbolServers)) {
                    assemblyPathMap.TryAdd(assemblyName.FullName, assemblyPath);
                    assemblyNames.Add(assemblyName);
                    DebuggerLoggingService.CustomLogger?.LogMessage($"User assembly '{assemblyName.Name}' added");
                }
            } catch (Exception e) {
                DebuggerLoggingService.CustomLogger?.LogError($"Error while processing assembly '{assemblyPath}'", e);
            }
        }

        startInfo.SymbolPathMap = assemblySymbolPathMap;
        startInfo.AssemblyPathMap = assemblyPathMap;
        startInfo.UserAssemblyNames = assemblyNames;
    }
}