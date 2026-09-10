using System.Collections.ObjectModel;
using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.Workspaces.Components;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis.MSBuild;
using DotRushMSBuildLocator = DotRush.Common.MSBuild.MSBuildLocator;

namespace DotRush.Roslyn.Workspaces;

public abstract class DotRushWorkspace : SolutionController {
    private MSBuildWorkspace? workspace;
    private ShadowCopyAnalyzerLoader? analyzerLoader;

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

    public async Task LoadAsync(IEnumerable<string> targets, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(workspace);
        await OnLoadingStartedAsync(cancellationToken);

        analyzerLoader?.Dispose();
        analyzerLoader = new ShadowCopyAnalyzerLoader();

        var solutionFiles = targets.Where(it => WorkspaceExtensions.IsSolutionFile(it)).Select(Path.GetFullPath).ToArray();
        if (solutionFiles.Length != 0)
            await LoadSolutionAsync(workspace, solutionFiles, analyzerLoader, cancellationToken);

        var projectFiles = targets.Where(it => WorkspaceExtensions.IsProjectFile(it)).Select(Path.GetFullPath).ToArray();
        if (projectFiles.Length != 0)
            await LoadProjectsAsync(workspace, projectFiles, analyzerLoader, cancellationToken);

        await OnLoadingCompletedAsync(cancellationToken);
        analyzerLoader?.Dispose();
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
