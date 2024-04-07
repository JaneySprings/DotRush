using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Server.Extensions;
using Microsoft.Extensions.Configuration;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DotRush.Roslyn.Server.Services;

public class ConfigurationService {
    private readonly ILanguageServerConfiguration configuration;

    private const string ExtensionId = "dotrush";
    private const string RoslynId = "roslyn";

    private bool? useRoslynAnalyzers;
    public bool UseRoslynAnalyzers => useRoslynAnalyzers ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:enableAnalyzers", false);

    private bool? showItemsFromUnimportedNamespaces;
    public bool ShowItemsFromUnimportedNamespaces => showItemsFromUnimportedNamespaces ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:showItemsFromUnimportedNamespaces", false);

    private bool? skipUnrecognizedProjects;
    public bool SkipUnrecognizedProjects => skipUnrecognizedProjects ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:skipUnrecognizedProjects", true);

    private bool? loadMetadataForReferencedProjects;
    public bool LoadMetadataForReferencedProjects => loadMetadataForReferencedProjects ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:loadMetadataForReferencedProjects", false);

    private Dictionary<string, string>? workspaceProperties;
    public Dictionary<string, string> WorkspaceProperties => workspaceProperties ??= configuration.GetKeyValuePairs($"{ExtensionId}:{RoslynId}:workspaceProperties");

    public ConfigurationService(ILanguageServerConfiguration configuration) {
        this.configuration = configuration;
    }

    public async Task InitializeAsync() {
        var retryCount = 0;
        await Task.Run(() => {
            while (!configuration.AsEnumerable().Any() && retryCount < 25) {
                Thread.Sleep(200);
                retryCount++;
            }
        }).ConfigureAwait(false);
        CurrentSessionLogger.Debug("ConfigurationService initialized");
    }
}