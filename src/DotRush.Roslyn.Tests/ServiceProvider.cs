using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Tests.Extensions;

namespace DotRush.Roslyn.Tests;

public static class ServiceProvider {
    public static WorkspaceService WorkspaceService { get; }
    public static TestConfigurationService ConfigurationService { get; }
    public static CodeAnalysisService CodeAnalysisService { get; }
    public static string SharedProjectPath { get; }
    public static string SharedProjectDirectory => Path.GetDirectoryName(SharedProjectPath)!;

    static ServiceProvider() {
        if (Directory.Exists(TestProjectExtensions.TestSharedProjectsDirectory))
            Directory.Delete(TestProjectExtensions.TestSharedProjectsDirectory, true);

        ConfigurationService = new TestConfigurationService();
        WorkspaceService = new WorkspaceService(ConfigurationService, null, null);
        CodeAnalysisService = new CodeAnalysisService(null, WorkspaceService, ConfigurationService);
        SharedProjectPath = TestProjectExtensions.CreateConsoleApp("SharedProjectApp", TestProjectExtensions.MultiTargetFramework, TestProjectExtensions.TestSharedProjectsDirectory);

        WorkspaceService.InitializeWorkspace();
        WorkspaceService.AddTargets([SharedProjectPath]);
        WorkspaceService.LoadSolutionAsync(CancellationToken.None).Wait();
    }
}
