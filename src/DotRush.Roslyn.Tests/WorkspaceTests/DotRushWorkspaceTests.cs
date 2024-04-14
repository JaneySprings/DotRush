using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Workspaces;
using Xunit;
using Xunit.Sdk;

namespace DotRush.Roslyn.Tests.WorkspaceTests;

public class DotRushWorkspaceTests : MSBuildTestFixture, IDisposable {

    public DotRushWorkspaceTests() {
        SafeExtensions.ThrowOnExceptions = true;
    }

    [Fact]
    public async Task LoadSimpleProjectTest() {
        var projectPath = CreateClassLib("MyClassLib");
        var workspace = new TestWorkspace([projectPath]);

        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(workspace.Solution);
        Assert.Single(workspace.Solution.ProjectIds);
        Assert.Equal("MyClassLib", workspace.Solution.Projects.First().Name);
        Assert.Equal(projectPath, workspace.Solution.Projects.First().FilePath);
    }
    [Fact]
    public async Task LoadProjectWithMultiTargetFrameworksTest() {
        var projectPath = CreateClassLib("MyClassLib", MultiTargetFramework);
        var workspace = new TestWorkspace([projectPath]);

        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(workspace.Solution);
        Assert.Equal(2, workspace.Solution.ProjectIds.Count);
        Assert.Equal(projectPath, workspace.Solution.Projects.ToArray()[0].FilePath);
        Assert.Equal(projectPath, workspace.Solution.Projects.ToArray()[1].FilePath);

        var projectNames = workspace.Solution.Projects.Select(p => p.Name);
        var targetNames = MultiTargetFramework.Split(';');
        foreach (var targetName in targetNames)
            Assert.Contains($"MyClassLib({targetName})", projectNames);
    }
    [Fact]
    public async Task GlobalPropertiesForProjectWithMultiTargetFrameworksTest() {
        var projectPath = CreateClassLib("MyClassLib", MultiTargetFramework);
        var workspace = new TestWorkspace([projectPath], new Dictionary<string, string> {
            { "TargetFramework", "net8.0" },
        });

        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(workspace.Solution);
        Assert.Single(workspace.Solution.ProjectIds);
        Assert.Equal("MyClassLib", workspace.Solution.Projects.First().Name);
    }
    [Fact]
    public async Task ErrorOnRestoreTest() {
        var projectPath = CreateProject(@"
<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <OutputType>Library</OutputType>
        <TargetFramework>Invalid</TargetFramework>
    </PropertyGroup>
</Project>
        ", "MyClassLib");
        var workspace = new TestWorkspace([projectPath]);
        await Assert.ThrowsAsync<FailException>(() => workspace.LoadSolutionAsync(CancellationToken.None)).ConfigureAwait(false);
    }
    [Fact]
    public async Task AutomaticProjectFinderTest() {
        var workspace = new TestWorkspace([]); 
        var invisibleDirectory = Path.Combine(MockProjectsDirectory, ".hidden");
        Directory.CreateDirectory(invisibleDirectory);

        var firstProject = CreateClassLib("MyClassLib");
        var secondProject = CreateConsoleApp("MyConsoleApp");
        var thirdProject = CreateClassLib("MyClassLib2", null, invisibleDirectory);

        workspace.FindTargetsInWorkspace([MockProjectsDirectory]);
        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(workspace.Solution);
        Assert.Equal(2, workspace.Solution.ProjectIds.Count);

        var projectNames = workspace.Solution.Projects.Select(p => p.Name);
        Assert.Contains("MyClassLib", projectNames);
        Assert.Contains("MyConsoleApp", projectNames);
        Assert.DoesNotContain("MyClassLib2", projectNames);
    }

    public void Dispose() {
        DeleteMockData();
    }
}

public class TestWorkspace : DotRushWorkspace {
    private readonly Dictionary<string, string> workspaceProperties;
    protected override Dictionary<string, string> WorkspaceProperties => workspaceProperties;

    private readonly bool loadMetadataForReferencedProjects;
    protected override bool LoadMetadataForReferencedProjects => loadMetadataForReferencedProjects;

    private readonly bool skipUnrecognizedProjects;
    protected override bool SkipUnrecognizedProjects => skipUnrecognizedProjects;

    private readonly bool restoreProjectsBeforeLoading;
    protected override bool RestoreProjectsBeforeLoading => restoreProjectsBeforeLoading;

    private readonly bool compileProjectsAfterLoading;
    protected override bool CompileProjectsAfterLoading => compileProjectsAfterLoading;

    public TestWorkspace(string[] targets, Dictionary<string, string>? workspaceProperties = null, bool loadMetadataForReferencedProjects = true, bool skipUnrecognizedProjects = false, bool restoreProjectsBeforeLoading = true, bool compileProjectsAfterLoading = true) {
        this.workspaceProperties = workspaceProperties ?? new Dictionary<string, string>();
        this.loadMetadataForReferencedProjects = loadMetadataForReferencedProjects;
        this.skipUnrecognizedProjects = skipUnrecognizedProjects;
        this.restoreProjectsBeforeLoading = restoreProjectsBeforeLoading;
        this.compileProjectsAfterLoading = compileProjectsAfterLoading;

        InitializeWorkspace(e => Assert.Fail(e.Message));
        AddTargets(targets);
    }

    public override void OnProjectRestoreFailed(string documentPath, int exitCode) {
        Assert.Fail($"[{documentPath}]: Project restore failed with exit code {exitCode}");
    }
}