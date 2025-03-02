using System.Collections.ObjectModel;
using DotRush.Common.Logging;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotRush.Roslyn.Workspaces;

public abstract class DotRushWorkspace : SolutionController {
    private MSBuildWorkspace? workspace;

    protected abstract ReadOnlyDictionary<string, string> WorkspaceProperties { get; }
    protected abstract bool LoadMetadataForReferencedProjects { get; }
    protected abstract bool SkipUnrecognizedProjects { get; }
    protected abstract bool ApplyWorkspaceChanges { get; }

    public bool InitializeWorkspace() {
        var registrationResult = TryRegisterDotNetEnvironment();
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

    private static bool TryRegisterDotNetEnvironment() {
        try {
            if (!MSBuildLocator.CanRegister || MSBuildLocator.IsRegistered)
                return true;

            var dotnetSdk = Environment.GetEnvironmentVariable("DOTNET_SDK_PATH"); //TODO: addd option in settings
            if (!string.IsNullOrEmpty(dotnetSdk)) {
                MSBuildLocator.RegisterMSBuildPath(dotnetSdk);
                return true;
            }

            MSBuildLocator.RegisterDefaults();
            return true;
        } catch (Exception e) {
            CurrentSessionLogger.Error(e);
            return false;
        }
    }
}
