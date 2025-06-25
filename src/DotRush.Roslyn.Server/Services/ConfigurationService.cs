using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.Server.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Model;

namespace DotRush.Roslyn.Server.Services;

public class ConfigurationService {
    private const string ConfigurationFileName = "dotrush.config.json";
    private readonly CurrentClassLogger currentClassLogger;
    private readonly JsonSerializerOptions jsonSerializerOptions;
    private RoslynSection? configuration;

    public bool ShowItemsFromUnimportedNamespaces => configuration?.ShowItemsFromUnimportedNamespaces ?? false;
    public bool TargetTypedCompletionFilter => configuration?.TargetTypedCompletionFilter ?? false;

    public bool SkipUnrecognizedProjects => configuration?.SkipUnrecognizedProjects ?? true;
    public bool LoadMetadataForReferencedProjects => configuration?.LoadMetadataForReferencedProjects ?? false;
    public bool RestoreProjectsBeforeLoading => configuration?.RestoreProjectsBeforeLoading ?? true;
    public bool CompileProjectsAfterLoading => configuration?.CompileProjectsAfterLoading ?? true;
    public bool ApplyWorkspaceChanges => configuration?.ApplyWorkspaceChanges ?? false;
    public AnalysisScope CompilerDiagnosticsScope => configuration?.CompilerDiagnosticsScope ?? AnalysisScope.Project;
    public AnalysisScope AnalyzerDiagnosticsScope => configuration?.AnalyzerDiagnosticsScope ?? AnalysisScope.Document;
    public DiagnosticsFormat DiagnosticsFormat => configuration?.DiagnosticsFormat ?? DiagnosticsFormat.NoHints;
    public string DotNetSdkDirectory => configuration?.DotNetSdkDirectory ?? Environment.GetEnvironmentVariable("DOTNET_SDK_PATH") ?? string.Empty;
    public ReadOnlyDictionary<string, string> WorkspaceProperties => (configuration?.WorkspaceProperties ?? new List<string>()).ToPropertiesDictionary();
    public ReadOnlyCollection<string> ProjectOrSolutionFiles => (configuration?.ProjectOrSolutionFiles ?? new List<string>()).AsReadOnly();
    public ReadOnlyCollection<string> AnalyzerAssemblies => (configuration?.AnalyzerAssemblies ?? new List<string>()).AsReadOnly();

    private readonly TaskCompletionSource initializeTaskSource;
    public Task InitializeTask => initializeTaskSource.Task;

    public ConfigurationService() {
        currentClassLogger = new CurrentClassLogger(nameof(ConfigurationService));
        jsonSerializerOptions = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        initializeTaskSource = new TaskCompletionSource();
        var configFilePath = Path.Combine(Environment.CurrentDirectory, ConfigurationFileName);
        if (!File.Exists(configFilePath))
            configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigurationFileName);
        if (File.Exists(configFilePath)) {
            currentClassLogger.Debug($"Configuration file found: '{configFilePath}'");
            var configuration = SafeExtensions.Invoke(() => JsonSerializer.Deserialize<ConfigurationSection>(File.ReadAllText(configFilePath), jsonSerializerOptions));
            ChangeConfiguration(configuration);
        }
    }

    public void ChangeConfiguration(LSPAny? sectionJson) {
        if (sectionJson?.Value is not JsonDocument jsonDocument) {
            currentClassLogger.Error("Configuration section is not a valid JSON document.");
            return;
        }

        var sections = SafeExtensions.Invoke(() => JsonSerializer.Deserialize<ConfigurationSection>(jsonDocument, jsonSerializerOptions));
        ChangeConfiguration(sections);
    }
    private void ChangeConfiguration(ConfigurationSection? section) {
        if (section?.DotRush?.Roslyn == null) {
            currentClassLogger.Error("Configuration section is not a valid document.");
            return;
        }

        configuration = section.DotRush.Roslyn;
        initializeTaskSource.TrySetResult();
        currentClassLogger.Debug("configuration updated");
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

    [JsonPropertyName("skipUnrecognizedProjects")]
    public bool SkipUnrecognizedProjects { get; set; }

    [JsonPropertyName("loadMetadataForReferencedProjects")]
    public bool LoadMetadataForReferencedProjects { get; set; }

    [JsonPropertyName("restoreProjectsBeforeLoading")]
    public bool RestoreProjectsBeforeLoading { get; set; }

    [JsonPropertyName("compileProjectsAfterLoading")]
    public bool CompileProjectsAfterLoading { get; set; }

    [JsonPropertyName("applyWorkspaceChanges")]
    public bool ApplyWorkspaceChanges { get; set; }

    [JsonPropertyName("compilerDiagnosticsScope")]
    public AnalysisScope CompilerDiagnosticsScope { get; set; }

    [JsonPropertyName("analyzerDiagnosticsScope")]
    public AnalysisScope AnalyzerDiagnosticsScope { get; set; }

    [JsonPropertyName("diagnosticsFormat")]
    public DiagnosticsFormat DiagnosticsFormat { get; set; }

    [JsonPropertyName("dotnetSdkDirectory")]
    public string? DotNetSdkDirectory { get; set; }

    [JsonPropertyName("workspaceProperties")]
    public List<string>? WorkspaceProperties { get; set; }

    [JsonPropertyName("projectOrSolutionFiles")]
    public List<string>? ProjectOrSolutionFiles { get; set; }

    [JsonPropertyName("analyzerAssemblies")]
    public List<string>? AnalyzerAssemblies { get; set; }
}