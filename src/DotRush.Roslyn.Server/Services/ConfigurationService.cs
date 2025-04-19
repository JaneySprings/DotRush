using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotRush.Common.Logging;
using DotRush.Roslyn.Server.Extensions;

namespace DotRush.Roslyn.Server.Services;

public class ConfigurationService {
    private RoslynSection? configuration;

    public bool ShowItemsFromUnimportedNamespaces => configuration?.ShowItemsFromUnimportedNamespaces ?? false;
    public bool SkipUnrecognizedProjects => configuration?.SkipUnrecognizedProjects ?? true;
    public bool LoadMetadataForReferencedProjects => configuration?.LoadMetadataForReferencedProjects ?? false;
    public bool RestoreProjectsBeforeLoading => configuration?.RestoreProjectsBeforeLoading ?? true;
    public bool CompileProjectsAfterLoading => configuration?.CompileProjectsAfterLoading ?? true;
    public bool ApplyWorkspaceChanges => configuration?.ApplyWorkspaceChanges ?? false;
    public bool UseMultitargetDiagnostics => configuration?.UseMultitargetDiagnostics ?? true;
    public bool EnableAnalyzers => configuration?.EnableAnalyzers ?? true;
    public bool ProjectScopeDiagnostics => configuration?.ProjectScopeDiagnostics ?? true;
    public string DotNetSdkDirectory => configuration?.DotNetSdkDirectory ?? Environment.GetEnvironmentVariable("DOTNET_SDK_PATH") ?? string.Empty;
    public ReadOnlyDictionary<string, string> WorkspaceProperties => (configuration?.WorkspaceProperties ?? new List<string>()).ToPropertiesDictionary();
    public ReadOnlyCollection<string> ProjectOrSolutionFiles => (configuration?.ProjectOrSolutionFiles ?? new List<string>()).AsReadOnly();

    public ConfigurationService() {}
    internal ConfigurationService(ConfigurationSection configuration) {
        this.configuration = configuration.Roslyn;
    }

    public async Task InitializeAsync() {
        var sectionList = await LanguageServer.Proxy.GetConfigurationAsync(Resources.ExtensionId, 3, CancellationToken.None).ConfigureAwait(false);
        var section = sectionList?.FirstOrDefault()?.Value;
        if (section == null) {
            CurrentSessionLogger.Error("Configuration section not found in the configuration file.");
            return;
        }

        var sections = JsonSerializer.Deserialize<ConfigurationSection>((JsonDocument)section);
        configuration = sections?.Roslyn;
        CurrentSessionLogger.Debug("ConfigurationService initialized");
    }
}

internal sealed class ConfigurationSection {
    [JsonPropertyName("roslyn")]
    public RoslynSection? Roslyn { get; set; }
    // Other sections that not related to lsp
}
internal sealed class RoslynSection {
    [JsonPropertyName("showItemsFromUnimportedNamespaces")]
    public bool ShowItemsFromUnimportedNamespaces { get; set; }

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

    [JsonPropertyName("useMultitargetDiagnostics")]
    public bool UseMultitargetDiagnostics { get; set; }

    [JsonPropertyName("enableAnalyzers")]
    public bool EnableAnalyzers { get; set; }

    [JsonPropertyName("projectScopeDiagnostics")]
    public bool ProjectScopeDiagnostics { get; set; }

    [JsonPropertyName("dotnetSdkDirectory")]
    public string? DotNetSdkDirectory { get; set; }

    [JsonPropertyName("workspaceProperties")]
    public List<string>? WorkspaceProperties { get; set; }

    [JsonPropertyName("projectOrSolutionFiles")]
    public List<string>? ProjectOrSolutionFiles { get; set; }
}