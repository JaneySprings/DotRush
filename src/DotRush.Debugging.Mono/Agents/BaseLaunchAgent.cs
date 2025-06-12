using System.Reflection;
using DotRush.Common.Interop;
using DotRush.Debugging.Mono.Extensions;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;

namespace DotRush.Debugging.Mono;

public abstract class BaseLaunchAgent {
    protected List<Action> Disposables { get; init; }
    protected LaunchConfiguration Configuration { get; init; }

    protected BaseLaunchAgent(LaunchConfiguration configuration) {
        Disposables = new List<Action>();
        Configuration = configuration;
    }

    public abstract void Prepare(DebugSession debugSession);
    public abstract void Connect(SoftDebuggerSession session);
    public abstract IEnumerable<string> GetUserAssemblies(IProcessLogger? logger);

    public void Dispose() {
        foreach (var disposable in Disposables) {
            try {
                disposable.Invoke();
                DebuggerLoggingService.CustomLogger?.LogMessage($"Disposing {disposable.Method.Name}");
            } catch (Exception ex) {
                DebuggerLoggingService.CustomLogger?.LogMessage($"Error while disposing {disposable.Method.Name}: {ex.Message}");
            }
        }

        Disposables.Clear();
    }

    protected void SetAssemblies(SoftDebuggerStartInfo startInfo, IProcessLogger? logger) {
        var options = Configuration.DebuggerSessionOptions;
        var useSymbolServers = options.SearchMicrosoftSymbolServer || options.SearchNuGetSymbolServer;
        var assemblyPathMap = new Dictionary<string, string>();
        var assemblySymbolPathMap = new Dictionary<string, string>();
        var assemblyNames = new List<AssemblyName>();

        foreach (var assemblyPath in GetUserAssemblies(logger)) {
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