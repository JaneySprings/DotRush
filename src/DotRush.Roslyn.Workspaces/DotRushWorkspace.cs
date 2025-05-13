using System.Collections.ObjectModel;
using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.Build.Locator;
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

    public void ApplyChanges() {
        ArgumentNullException.ThrowIfNull(workspace);
        if (Solution != null && ApplyWorkspaceChanges) {
            workspace.TryApplyChanges(Solution);
            OnWorkspaceStateChanged(workspace.CurrentSolution);
        }
    }

    public Task LoadSolutionAsync(IEnumerable<string> solutionFiles, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(workspace);
        return LoadSolutionAsync(workspace, solutionFiles, cancellationToken);
    }
    public Task LoadProjectsAsync(IEnumerable<string> projectFiles, CancellationToken cancellationToken) {
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
        var registrationResult = SafeExtensions.Invoke(false, () => {
            if (!MSBuildLocator.CanRegister || MSBuildLocator.IsRegistered)
                return true;
            if (!string.IsNullOrEmpty(DotNetSdkDirectory)) {
                CurrentSessionLogger.Debug($"Registering MSBuild path: {DotNetSdkDirectory}");
                MSBuildLocator.RegisterMSBuildPath(DotNetSdkDirectory);
                return true;
            }
            MSBuildLocator.RegisterDefaults();
            return true;
        });

        if (registrationResult)
            return true;

        CurrentSessionLogger.Error("Faied to register MSBuild path. Trying to register the latest SDK path.");
        registrationResult = SafeExtensions.Invoke(false, () => {
            var latestSdkPath = DotRushMSBuildLocator.GetLatestSdkLocation();
            if (string.IsNullOrEmpty(latestSdkPath))
                return false;
            
            CurrentSessionLogger.Debug($"Registering MSBuild path: {latestSdkPath}");
            MSBuildLocator.RegisterMSBuildPath(latestSdkPath);
            return true;
        });

        return registrationResult;
    }
}
