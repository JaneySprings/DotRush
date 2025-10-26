using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotRush.Common;
using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.Server.Dispatchers;
using DotRush.Roslyn.Server.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Server;
using EmmyLua.LanguageServer.Framework.Server.Scheduler;

namespace DotRush.Roslyn.Server.Services;

public class ConfigurationService {
    private const string ConfigurationFileName = "dotrush.config.json";
    private readonly CurrentClassLogger currentClassLogger;
    private readonly LanguageServer? languageServer;
    private RoslynSection configuration;

    public bool ShowItemsFromUnimportedNamespaces => configuration.ShowItemsFromUnimportedNamespaces;
    public bool TargetTypedCompletionFilter => configuration.TargetTypedCompletionFilter;
    public bool TriggerCompletionOnSpace => configuration.TriggerCompletionOnSpace;

    public bool SkipUnrecognizedProjects => configuration.SkipUnrecognizedProjects;
    public bool LoadMetadataForReferencedProjects => configuration.LoadMetadataForReferencedProjects;
    public bool RestoreProjectsBeforeLoading => configuration.RestoreProjectsBeforeLoading;
    public bool CompileProjectsAfterLoading => configuration.CompileProjectsAfterLoading;
    public bool ApplyWorkspaceChanges => configuration.ApplyWorkspaceChanges;
    public AnalysisScope CompilerDiagnosticsScope => configuration.CompilerDiagnosticsScope;
    public AnalysisScope AnalyzerDiagnosticsScope => configuration.AnalyzerDiagnosticsScope;
    public DiagnosticsFormat DiagnosticsFormat => configuration.DiagnosticsFormat;
    public string DotNetSdkDirectory => configuration.DotNetSdkDirectory ?? Environment.GetEnvironmentVariable("DOTNET_SDK_PATH") ?? string.Empty;
    public ReadOnlyDictionary<string, string> WorkspaceProperties => (configuration.WorkspaceProperties ?? new List<string>()).ToPropertiesDictionary();
    public ReadOnlyCollection<string> ProjectOrSolutionFiles => (configuration.ProjectOrSolutionFiles ?? new List<string>()).AsReadOnly();
    public ReadOnlyCollection<string> AnalyzerAssemblies => (configuration.AnalyzerAssemblies ?? new List<string>()).AsReadOnly();

    private readonly TaskCompletionSource initializeTaskSource;
    public Task InitializeTask => initializeTaskSource.Task;

    public ConfigurationService(LanguageServer? serverFacade) {
        languageServer = serverFacade;
        configuration = new RoslynSection();
        initializeTaskSource = new TaskCompletionSource();
        currentClassLogger = new CurrentClassLogger(nameof(ConfigurationService));

        var configFilePath = Path.Combine(Environment.CurrentDirectory, ConfigurationFileName);
        if (!File.Exists(configFilePath))
            configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigurationFileName);
        if (File.Exists(configFilePath)) {
            currentClassLogger.Debug($"Configuration file found: '{configFilePath}'");
            var configuration = SafeExtensions.Invoke(() => JsonSerializer.Deserialize<ConfigurationSection>(File.ReadAllText(configFilePath), JsonSerializerConfig.Options));
            ChangeConfiguration(configuration);
        }
    }

    public void ChangeConfiguration(LSPAny? sectionJson) {
        if (sectionJson?.Value is not JsonDocument jsonDocument) {
            currentClassLogger.Error("Configuration section is not a valid JSON document.");
            return;
        }

        var sections = SafeExtensions.Invoke(() => JsonSerializer.Deserialize<ConfigurationSection>(jsonDocument, JsonSerializerConfig.Options));
        ChangeConfiguration(sections);
    }
    private void ChangeConfiguration(ConfigurationSection? section) {
        if (section?.DotRush?.Roslyn == null) {
            currentClassLogger.Error("Configuration section is not a valid document.");
            return;
        }

        configuration = section.DotRush.Roslyn;
        initializeTaskSource.TrySetResult();
        UpdateServerDispatcher(configuration.DispatcherType);
        currentClassLogger.Debug("configuration updated");
    }
    private void UpdateServerDispatcher(DispatcherType dispatcherType) {
        if (languageServer == null)
            return;

        currentClassLogger.Debug($"Setting server dispatcher type: {dispatcherType}");
        switch (dispatcherType) {
            case DispatcherType.SingleThread:
                languageServer.SetScheduler(new SingleThreadScheduler());
                break;
            case DispatcherType.MultiThread:
                languageServer.SetScheduler(new MultiThreadDispatcher());
                break;
            case DispatcherType.PerformanceCounter:
                languageServer.SetScheduler(new PerformanceCounterDispatcher());
                break;
            default:
                currentClassLogger.Error($"Unknown dispatcher type: {dispatcherType}");
                break;
        }

    }
}

internal sealed class ConfigurationSection {
    [JsonPropertyName("dotrush")]
    public DotRushSection? DotRush { get; set; }
    // Other sections that not related to lsp
}
internal sealed class DotRushSection {
    [JsonPropertyName("roslyn")]
    public RoslynSection? Roslyn { get; set; }
}
internal sealed class RoslynSection {
    [JsonPropertyName("showItemsFromUnimportedNamespaces")]
    public bool ShowItemsFromUnimportedNamespaces { get; set; }

    [JsonPropertyName("targetTypedCompletionFilter")]
    public bool TargetTypedCompletionFilter { get; set; }

    [JsonPropertyName("triggerCompletionOnSpace")]
    public bool TriggerCompletionOnSpace { get; set; }

    [JsonPropertyName("skipUnrecognizedProjects")]
    public bool SkipUnrecognizedProjects { get; set; } = true;

    [JsonPropertyName("loadMetadataForReferencedProjects")]
    public bool LoadMetadataForReferencedProjects { get; set; }

    [JsonPropertyName("restoreProjectsBeforeLoading")]
    public bool RestoreProjectsBeforeLoading { get; set; } = true;

    [JsonPropertyName("compileProjectsAfterLoading")]
    public bool CompileProjectsAfterLoading { get; set; } = true;

    [JsonPropertyName("applyWorkspaceChanges")]
    public bool ApplyWorkspaceChanges { get; set; }

    [JsonPropertyName("compilerDiagnosticsScope")]
    public AnalysisScope CompilerDiagnosticsScope { get; set; }

    [JsonPropertyName("analyzerDiagnosticsScope")]
    public AnalysisScope AnalyzerDiagnosticsScope { get; set; }

    [JsonPropertyName("diagnosticsFormat")]
    public DiagnosticsFormat DiagnosticsFormat { get; set; }

    [JsonPropertyName("dispatcherType")]
    public DispatcherType DispatcherType { get; set; }

    [JsonPropertyName("dotnetSdkDirectory")]
    public string? DotNetSdkDirectory { get; set; }

    [JsonPropertyName("workspaceProperties")]
    public List<string>? WorkspaceProperties { get; set; }

    [JsonPropertyName("projectOrSolutionFiles")]
    public List<string>? ProjectOrSolutionFiles { get; set; }

    [JsonPropertyName("analyzerAssemblies")]
    public List<string>? AnalyzerAssemblies { get; set; }
}

public enum DiagnosticsFormat {
    NoHints,
    InfosAsHints,
    AsIs,
}
public enum DispatcherType {
    MultiThread,
    SingleThread,
    PerformanceCounter
}