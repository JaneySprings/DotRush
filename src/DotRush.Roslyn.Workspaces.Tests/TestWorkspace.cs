using System.Collections.ObjectModel;
using DotRush.Common.External;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace DotRush.Roslyn.Workspaces.Tests;

public class TestWorkspace : DotRushWorkspace {
    private readonly ReadOnlyDictionary<string, string> workspaceProperties;
    private readonly bool loadMetadataForReferencedProjects;
    private readonly bool skipUnrecognizedProjects;
    private readonly bool restoreProjectsBeforeLoading;
    private readonly bool compileProjectsAfterLoading;
    private readonly bool applyWorkspaceChanges;

    private readonly List<string> loadedProjects = new List<string>();

    protected override ReadOnlyDictionary<string, string> WorkspaceProperties => workspaceProperties;
    protected override bool LoadMetadataForReferencedProjects => loadMetadataForReferencedProjects;
    protected override bool SkipUnrecognizedProjects => skipUnrecognizedProjects;
    protected override bool RestoreProjectsBeforeLoading => restoreProjectsBeforeLoading;
    protected override bool CompileProjectsAfterLoading => compileProjectsAfterLoading;
    protected override bool ApplyWorkspaceChanges => applyWorkspaceChanges;
    protected override string DotNetSdkDirectory => string.Empty;


    public TestWorkspace(Dictionary<string, string>? workspaceProperties = null) : this(workspaceProperties, true, false, false, false, false) {}
    public TestWorkspace(bool restore) : this(null, true, false, restore, false, false) {}
    public TestWorkspace(Dictionary<string, string>? workspaceProperties, bool loadMetadataForReferencedProjects, bool skipUnrecognizedProjects, bool restoreProjectsBeforeLoading, bool compileProjectsAfterLoading , bool applyWorkspaceChanges) {
        this.workspaceProperties = new ReadOnlyDictionary<string, string>(workspaceProperties ?? new Dictionary<string, string>());
        this.loadMetadataForReferencedProjects = loadMetadataForReferencedProjects;
        this.skipUnrecognizedProjects = skipUnrecognizedProjects;
        this.restoreProjectsBeforeLoading = restoreProjectsBeforeLoading;
        this.compileProjectsAfterLoading = compileProjectsAfterLoading;
        this.applyWorkspaceChanges = applyWorkspaceChanges;

        if (!InitializeWorkspace())
            throw new InvalidOperationException("Failed to initialize workspace");
    }

    public override void OnProjectRestoreFailed(string documentPath, ProcessResult result) {
        throw new InvalidOperationException($"[{documentPath}]: Project restore failed with exit code {result.ExitCode}:\nOutput: {result.GetOutput()}\nError: {result.GetError()}");
    }
    public override void OnProjectLoadCompleted(string documentPath) {
        loadedProjects.Add(documentPath);
    }

    public void SetSolution(Solution solution) {
        Solution = solution;
    }
    public void AssertLoadedProjects(int expectedCount) {
        Assert.That(loadedProjects, Has.Count.EqualTo(expectedCount));

        var projects = Solution!.Projects.Select(it => it.FilePath).Distinct().ToList();
        projects.ForEach(path => Assert.That(loadedProjects, Contains.Item(path)));
    }
}