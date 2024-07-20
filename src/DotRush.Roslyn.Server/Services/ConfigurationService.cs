using System.Collections.ObjectModel;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Server.Extensions;
using Microsoft.Extensions.Configuration;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DotRush.Roslyn.Server.Services;

public interface IConfigurationService {
    bool ShowItemsFromUnimportedNamespaces { get; }
    bool SkipUnrecognizedProjects { get; }
    bool LoadMetadataForReferencedProjects { get; }
    bool RestoreProjectsBeforeLoading { get; }
    bool CompileProjectsAfterLoading { get; }
    Dictionary<string, string> WorkspaceProperties { get; }
    ReadOnlyCollection<string> ProjectFiles { get; }
}

public class ConfigurationService : IConfigurationService {
    private readonly ILanguageServerConfiguration configuration;
    private const string ExtensionId = "dotrush";
    private const string RoslynId = "roslyn";

    private bool? showItemsFromUnimportedNamespaces;
    bool IConfigurationService.ShowItemsFromUnimportedNamespaces => showItemsFromUnimportedNamespaces ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:showItemsFromUnimportedNamespaces", false);

    private bool? skipUnrecognizedProjects;
    bool IConfigurationService.SkipUnrecognizedProjects => skipUnrecognizedProjects ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:skipUnrecognizedProjects", true);

    private bool? loadMetadataForReferencedProjects;
    bool IConfigurationService.LoadMetadataForReferencedProjects => loadMetadataForReferencedProjects ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:loadMetadataForReferencedProjects", false);

    private bool? restoreProjectsBeforeLoading;
    bool IConfigurationService.RestoreProjectsBeforeLoading => restoreProjectsBeforeLoading ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:restoreProjectsBeforeLoading", true);

    private bool? compileProjectsAfterLoading;
    bool IConfigurationService.CompileProjectsAfterLoading => compileProjectsAfterLoading ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:compileProjectsAfterLoading", true);

    private Dictionary<string, string>? workspaceProperties;
    Dictionary<string, string> IConfigurationService.WorkspaceProperties => workspaceProperties ??= configuration.GetKeyValuePairs($"{ExtensionId}:{RoslynId}:workspaceProperties");

    private ReadOnlyCollection<string>? projectFiles;
    public ReadOnlyCollection<string> ProjectFiles => projectFiles ??= configuration.GetArray($"{ExtensionId}:{RoslynId}:projectFiles");

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