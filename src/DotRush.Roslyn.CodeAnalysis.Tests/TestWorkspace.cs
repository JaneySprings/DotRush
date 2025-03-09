using System.Collections.ObjectModel;
using DotRush.Roslyn.Workspaces;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.CodeAnalysis.Tests;

public class TestWorkspace : DotRushWorkspace {
    private readonly ReadOnlyDictionary<string, string> workspaceProperties;
    private readonly bool loadMetadataForReferencedProjects;
    private readonly bool skipUnrecognizedProjects;
    private readonly bool restoreProjectsBeforeLoading;
    private readonly bool compileProjectsAfterLoading;
    private readonly bool applyWorkspaceChanges;

    protected override ReadOnlyDictionary<string, string> WorkspaceProperties => workspaceProperties;
    protected override bool LoadMetadataForReferencedProjects => loadMetadataForReferencedProjects;
    protected override bool SkipUnrecognizedProjects => skipUnrecognizedProjects;
    protected override bool RestoreProjectsBeforeLoading => restoreProjectsBeforeLoading;
    protected override bool CompileProjectsAfterLoading => compileProjectsAfterLoading;
    protected override bool ApplyWorkspaceChanges => applyWorkspaceChanges;
    protected override string DotNetSdkDirectory => string.Empty;


    public TestWorkspace(Dictionary<string, string>? workspaceProperties = null) : this(workspaceProperties, true, false, true, false, false) {}
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

    public async Task LoadAsync(string[] targets, CancellationToken cancellationToken) {
        var solutionFiles = targets.Where(it => 
            Path.GetExtension(it).Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(it).Equals(".slnf", StringComparison.OrdinalIgnoreCase)
        );
        if (solutionFiles.Any())
            await LoadSolutionAsync(solutionFiles, cancellationToken).ConfigureAwait(false);

        var projectFiles = targets.Where(it => Path.GetExtension(it).Equals(".csproj", StringComparison.OrdinalIgnoreCase));
        if (projectFiles.Any())
            await LoadProjectsAsync(projectFiles, cancellationToken).ConfigureAwait(false);
    }
}