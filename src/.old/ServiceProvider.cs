using System.Collections.ObjectModel;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Tests.Extensions;

namespace DotRush.Roslyn.Tests;

public static class ServiceProvider {
    public static WorkspaceService WorkspaceService { get; }
    public static ConfigurationService ConfigurationService { get; }
    public static CodeAnalysisService CodeAnalysisService { get; }
    public static string SharedProjectPath { get; }
    public static string SharedProjectDirectory => Path.GetDirectoryName(SharedProjectPath)!;

    static ServiceProvider() {
        if (Directory.Exists(TestProjectExtensions.TestSharedProjectsDirectory))
            Directory.Delete(TestProjectExtensions.TestSharedProjectsDirectory, true);

        ConfigurationService = new ConfigurationService(
            showItemsFromUnimportedNamespaces: true,
            skipUnrecognizedProjects: true,
            loadMetadataForReferencedProjects: true,
            restoreProjectsBeforeLoading: true,
            compileProjectsAfterLoading: true,
            useMultitargetDiagnostics: true,
            workspaceProperties: new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()),
            projectFiles: new ReadOnlyCollection<string>(new List<string>()),
            excludePatterns: new ReadOnlyCollection<string>(new List<string>())
        );
        WorkspaceService = new WorkspaceService(ConfigurationService, null, null);
        CodeAnalysisService = new CodeAnalysisService(null, ConfigurationService, WorkspaceService);
        SharedProjectPath = TestProjectExtensions.CreateConsoleApp("SharedProjectApp", TestProjectExtensions.MultiTargetFramework, TestProjectExtensions.TestSharedProjectsDirectory);

        WorkspaceService.InitializeWorkspace();
        WorkspaceService.AddTargets([SharedProjectPath]);
        WorkspaceService.LoadSolutionAsync(CancellationToken.None).Wait();
    }
}
