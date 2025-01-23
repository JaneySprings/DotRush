using System.Collections.ObjectModel;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Server.Extensions;
using Microsoft.Extensions.Configuration;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DotRush.Roslyn.Server.Services;

public class ConfigurationService {
    private readonly ILanguageServerConfiguration? configuration;
    private const string ExtensionId = "dotrush";
    private const string RoslynId = "roslyn";

    private bool? showItemsFromUnimportedNamespaces;
    public bool ShowItemsFromUnimportedNamespaces => showItemsFromUnimportedNamespaces ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:showItemsFromUnimportedNamespaces", false);

    private bool? skipUnrecognizedProjects;
    public bool SkipUnrecognizedProjects => skipUnrecognizedProjects ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:skipUnrecognizedProjects", true);

    private bool? loadMetadataForReferencedProjects;
    public bool LoadMetadataForReferencedProjects => loadMetadataForReferencedProjects ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:loadMetadataForReferencedProjects", false);

    private bool? restoreProjectsBeforeLoading;
    public bool RestoreProjectsBeforeLoading => restoreProjectsBeforeLoading ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:restoreProjectsBeforeLoading", true);

    private bool? compileProjectsAfterLoading;
    public bool CompileProjectsAfterLoading => compileProjectsAfterLoading ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:compileProjectsAfterLoading", true);

    private bool? applyWorkspaceChanges;
    public bool ApplyWorkspaceChanges => applyWorkspaceChanges ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:applyWorkspaceChanges", false);

    private bool? useMultitargetDiagnostics;
    public bool UseMultitargetDiagnostics => useMultitargetDiagnostics ??= configuration.GetValue($"{ExtensionId}:{RoslynId}:useMultitargetDiagnostics", true);

    private ReadOnlyDictionary<string, string>? workspaceProperties;
    public ReadOnlyDictionary<string, string> WorkspaceProperties => workspaceProperties ??= ServerExtensions.GetKeyValuePairs(configuration, $"{ExtensionId}:{RoslynId}:workspaceProperties");

    private ReadOnlyCollection<string>? projectOrSolutionFiles;
    public ReadOnlyCollection<string> ProjectOrSolutionFiles => projectOrSolutionFiles ??= ServerExtensions.GetArray(configuration, $"{ExtensionId}:{RoslynId}:projectOrSolutionFiles");


    public ConfigurationService(ILanguageServerConfiguration configuration) {
        this.configuration = configuration;
    }
    internal ConfigurationService(
        bool showItemsFromUnimportedNamespaces,
        bool skipUnrecognizedProjects,
        bool loadMetadataForReferencedProjects,
        bool restoreProjectsBeforeLoading,
        bool compileProjectsAfterLoading,
        bool useMultitargetDiagnostics,
        ReadOnlyDictionary<string, string> workspaceProperties,
        ReadOnlyCollection<string> projectOrSolutionFiles
    ) {
        this.showItemsFromUnimportedNamespaces = showItemsFromUnimportedNamespaces;
        this.skipUnrecognizedProjects = skipUnrecognizedProjects;
        this.loadMetadataForReferencedProjects = loadMetadataForReferencedProjects;
        this.restoreProjectsBeforeLoading = restoreProjectsBeforeLoading;
        this.compileProjectsAfterLoading = compileProjectsAfterLoading;
        this.useMultitargetDiagnostics = useMultitargetDiagnostics;
        this.workspaceProperties = workspaceProperties;
        this.projectOrSolutionFiles = projectOrSolutionFiles;
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