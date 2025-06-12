using System.Collections.ObjectModel;
using DotRush.Common.InteropV2;
using DotRush.Roslyn.Workspaces.FileSystem;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace DotRush.Roslyn.Workspaces.Tests;

public class TestWorkspace : DotRushWorkspace, IWorkspaceChangeListener {
    private readonly ReadOnlyDictionary<string, string> workspaceProperties;
    private readonly bool loadMetadataForReferencedProjects;
    private readonly bool skipUnrecognizedProjects;
    private readonly bool restoreProjectsBeforeLoading;
    private readonly bool compileProjectsAfterLoading;
    private readonly bool applyWorkspaceChanges;

    private readonly List<string> loadedProjects = new List<string>();
    public List<string> CreatedDocuments { get; } = new List<string>();
    public List<string> UpdatedDocuments { get; } = new List<string>();
    public List<string> DeletedDocuments { get; } = new List<string>();

    protected override ReadOnlyDictionary<string, string> WorkspaceProperties => workspaceProperties;
    protected override bool LoadMetadataForReferencedProjects => loadMetadataForReferencedProjects;
    protected override bool SkipUnrecognizedProjects => skipUnrecognizedProjects;
    protected override bool RestoreProjectsBeforeLoading => restoreProjectsBeforeLoading;
    protected override bool CompileProjectsAfterLoading => compileProjectsAfterLoading;
    protected override bool ApplyWorkspaceChanges => applyWorkspaceChanges;
    protected override string DotNetSdkDirectory => string.Empty;

    bool IWorkspaceChangeListener.IsGitEventsSupported => false;

    public TestWorkspace(
        Dictionary<string, string>? workspaceProperties = null,
        bool loadMetadataForReferencedProjects = false,
        bool skipUnrecognizedProjects = false,
        bool restoreProjectsBeforeLoading = false,
        bool compileProjectsAfterLoading = false,
        bool applyWorkspaceChanges = false
    ) {
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
    public override void OnProjectLoadCompleted(Project project) {
        loadedProjects.Add(project.FilePath!);
    }

    public void SetSolution(Solution solution) {
        Solution = solution;
    }
    public void AssertLoadedProjects(int expectedCount) {
        Assert.That(loadedProjects, Has.Count.EqualTo(expectedCount));

        var projects = Solution!.Projects.Select(it => it.FilePath).Distinct().ToList();
        projects.ForEach(path => Assert.That(loadedProjects, Contains.Item(path)));
    }

    void IWorkspaceChangeListener.OnDocumentsCreated(IEnumerable<string> documentPaths) {
        CreateDocuments(documentPaths.ToArray());
        CreatedDocuments.AddRange(documentPaths);
    }
    void IWorkspaceChangeListener.OnDocumentsDeleted(IEnumerable<string> documentPaths) {
        DeleteDocuments(documentPaths.ToArray());
        DeletedDocuments.AddRange(documentPaths);
    }
    void IWorkspaceChangeListener.OnDocumentsChanged(IEnumerable<string> documentPaths) {
        UpdateDocuments(documentPaths.ToArray());
        UpdatedDocuments.AddRange(documentPaths);
    }
    void IWorkspaceChangeListener.OnCommitChanges() { }
}