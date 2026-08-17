using System.Collections.ObjectModel;
using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis.MSBuild;
using DotRushMSBuildLocator = DotRush.Common.MSBuild.MSBuildLocator;

namespace DotRush.Roslyn.Workspaces;

public abstract class DotRushWorkspace : SolutionController {
    private MSBuildWorkspace? workspace;

    protected abstract ReadOnlyDictionary<string, string> WorkspaceProperties { get; }
    protected abstract bool LoadMetadataForReferencedProjects { get; }
    protected abstract bool SkipUnrecognizedProjects { get; }
    protected abstract bool ApplyWorkspaceChanges { get; }
    protected abstract string DotNetSdkDirectory { get; }

    public bool InitializeWorkspace() {
        var registrationResult = TryRegisterDotNetEnvironment();
        if (workspace != null)
            workspace.Dispose();

        workspace = MSBuildWorkspace.Create(WorkspaceProperties);
        workspace.LoadMetadataForReferencedProjects = LoadMetadataForReferencedProjects;
        workspace.SkipUnrecognizedProjects = SkipUnrecognizedProjects;
        return registrationResult;
    }

    public Task LoadSolutionAsync(string[] solutionFiles, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(workspace);
        return LoadSolutionAsync(workspace, solutionFiles, cancellationToken);
    }
    public Task LoadProjectsAsync(string[] projectFiles, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(workspace);
        return LoadProjectsAsync(workspace, projectFiles, cancellationToken);
    }
    public async Task LoadAsync(IEnumerable<string> targets, CancellationToken cancellationToken) {
        await OnLoadingStartedAsync(cancellationToken);

        var solutionFiles = targets.Where(it => WorkspaceExtensions.IsSolutionFile(it)).Select(Path.GetFullPath).ToArray();
        if (solutionFiles.Length != 0)
            await LoadSolutionAsync(solutionFiles, cancellationToken).ConfigureAwait(false);

        var projectFiles = targets.Where(it => WorkspaceExtensions.IsProjectFile(it)).Select(Path.GetFullPath).ToArray();
        if (projectFiles.Length != 0)
            await LoadProjectsAsync(projectFiles, cancellationToken).ConfigureAwait(false);

        await OnLoadingCompletedAsync(cancellationToken);
    }

    private bool TryRegisterDotNetEnvironment() {
        return SafeExtensions.Invoke(false, () => {
            if (string.IsNullOrEmpty(DotNetSdkDirectory))
                return true;

            CurrentSessionLogger.Debug($"Registering MSBuild path: {DotNetSdkDirectory}");
            DotRushMSBuildLocator.RegisterMSBuildPath(DotNetSdkDirectory);
            return true;
        });
    }
}
