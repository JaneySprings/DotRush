using System.Collections.ObjectModel;
using DotRush.Roslyn.Common.External;
using DotRush.Roslyn.Workspaces;
using Microsoft.CodeAnalysis;
using Xunit;

namespace DotRush.Roslyn.Tests.WorkspaceTests;

public class TestWorkspace : DotRushWorkspace {
    private readonly ReadOnlyDictionary<string, string> workspaceProperties;
    private readonly bool loadMetadataForReferencedProjects;
    private readonly bool skipUnrecognizedProjects;
    private readonly bool restoreProjectsBeforeLoading;
    private readonly bool compileProjectsAfterLoading;

    protected override ReadOnlyDictionary<string, string> WorkspaceProperties => workspaceProperties;
    protected override bool LoadMetadataForReferencedProjects => loadMetadataForReferencedProjects;
    protected override bool SkipUnrecognizedProjects => skipUnrecognizedProjects;
    protected override bool RestoreProjectsBeforeLoading => restoreProjectsBeforeLoading;
    protected override bool CompileProjectsAfterLoading => compileProjectsAfterLoading;

    public TestWorkspace(string[] targets, Dictionary<string, string>? workspaceProperties = null, bool loadMetadataForReferencedProjects = true, bool skipUnrecognizedProjects = false, bool restoreProjectsBeforeLoading = true, bool compileProjectsAfterLoading = false) {
        this.workspaceProperties = new ReadOnlyDictionary<string, string>(workspaceProperties ?? new Dictionary<string, string>());
        this.loadMetadataForReferencedProjects = loadMetadataForReferencedProjects;
        this.skipUnrecognizedProjects = skipUnrecognizedProjects;
        this.restoreProjectsBeforeLoading = restoreProjectsBeforeLoading;
        this.compileProjectsAfterLoading = compileProjectsAfterLoading;

        if (!InitializeWorkspace())
            Assert.Fail("Failed to initialize workspace");
    
        AddTargets(targets);
    }

    public override void OnProjectRestoreFailed(string documentPath, ProcessResult result) {
        Assert.Fail($"[{documentPath}]: Project restore failed with exit code {result.ExitCode}");
    }

    public void SetSolution(Solution solution) {
        Solution = solution;
    }
}