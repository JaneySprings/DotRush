using DotRush.Roslyn.Workspaces;
using Xunit;

namespace DotRush.Roslyn.Tests.WorkspaceTests;

public class TestWorkspace : DotRushWorkspace {
    private readonly Dictionary<string, string> workspaceProperties;
    private readonly bool loadMetadataForReferencedProjects;
    private readonly bool skipUnrecognizedProjects;
    private readonly bool restoreProjectsBeforeLoading;
    private readonly bool compileProjectsAfterLoading;

    protected override Dictionary<string, string> WorkspaceProperties => workspaceProperties;
    protected override bool LoadMetadataForReferencedProjects => loadMetadataForReferencedProjects;
    protected override bool SkipUnrecognizedProjects => skipUnrecognizedProjects;
    protected override bool RestoreProjectsBeforeLoading => restoreProjectsBeforeLoading;
    protected override bool CompileProjectsAfterLoading => compileProjectsAfterLoading;

    public TestWorkspace(string[] targets, Dictionary<string, string>? workspaceProperties = null, bool loadMetadataForReferencedProjects = true, bool skipUnrecognizedProjects = false, bool restoreProjectsBeforeLoading = true, bool compileProjectsAfterLoading = false) {
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