using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Server.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Configuration;

namespace DotRush.Roslyn.Server.Services;

public class ConfigurationService {
    private const string ExtensionId = "dotrush";
    private const string RoslynId = "roslyn";
    
    private Configuration? configuration;

    public bool ShowItemsFromUnimportedNamespaces => configuration?.ShowItemsFromUnimportedNamespaces ?? false;
    public bool SkipUnrecognizedProjects => configuration?.SkipUnrecognizedProjects ?? true;
    public bool LoadMetadataForReferencedProjects => configuration?.LoadMetadataForReferencedProjects ?? false;
    public bool RestoreProjectsBeforeLoading => configuration?.RestoreProjectsBeforeLoading ?? true;
    public bool CompileProjectsAfterLoading => configuration?.CompileProjectsAfterLoading ?? true;
    public bool ApplyWorkspaceChanges => configuration?.ApplyWorkspaceChanges ?? false;
    public bool UseMultitargetDiagnostics => configuration?.UseMultitargetDiagnostics ?? true;
    public ReadOnlyDictionary<string, string> WorkspaceProperties => (configuration?.WorkspaceProperties ?? new List<string>()).ToPropertiesDictionary();
    public ReadOnlyCollection<string> ProjectOrSolutionFiles => (configuration?.ProjectOrSolutionFiles ?? new List<string>()).AsReadOnly();

    public ConfigurationService() {}
    internal ConfigurationService(Configuration configuration) {
        this.configuration = configuration;
    }

    public async Task InitializeAsync() {
        var sectionList = await LanguageServer.Proxy.GetConfiguration(new ConfigurationParams { 
            Items = new List<ConfigurationItem> { new ConfigurationItem { Section = $"{ExtensionId}.{RoslynId}" }
        }}, CancellationToken.None).ConfigureAwait(false);

        var section = sectionList.FirstOrDefault()?.Value;
        if (section == null) {
            CurrentSessionLogger.Error("ConfigurationService failed to initialize");
            return;
        }

        configuration = JsonSerializer.Deserialize<Configuration>((JsonDocument)section);
        CurrentSessionLogger.Debug("ConfigurationService initialized");
    }
}

internal class Configuration {
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

    [JsonPropertyName("workspaceProperties")]
    public List<string>? WorkspaceProperties { get; set; }
    
    [JsonPropertyName("projectOrSolutionFiles")]
    public List<string>? ProjectOrSolutionFiles { get; set; }
}