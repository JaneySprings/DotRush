using System.Collections.ObjectModel;
using DotRush.Roslyn.Common.Logging;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotRush.Roslyn.Workspaces;

public abstract class DotRushWorkspace : SolutionController {
    private MSBuildWorkspace? workspace;

    protected abstract ReadOnlyDictionary<string, string> WorkspaceProperties { get; }
    protected abstract bool LoadMetadataForReferencedProjects { get; }
    protected abstract bool SkipUnrecognizedProjects { get; }

    public bool InitializeWorkspace() {
        var registrationResult = TryRegisterDotNetEnvironment();
        workspace = MSBuildWorkspace.Create(WorkspaceProperties);
        workspace.LoadMetadataForReferencedProjects = LoadMetadataForReferencedProjects;
        workspace.SkipUnrecognizedProjects = SkipUnrecognizedProjects;
        return registrationResult;
    }

    protected override void OnApplyChangesRequested(Solution? newSolution) {
        // ArgumentNullException.ThrowIfNull(workspace);
        // if (newSolution != null /*&& SomeTrueExpression*/)
        //     workspace.TryApplyChanges(newSolution);

        // OnWorkspaceStateChanged(workspace.CurrentSolution);
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

            var dotnetPath = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (!string.IsNullOrEmpty(dotnetPath)) {
                MSBuildLocator.RegisterMSBuildPath(dotnetPath);
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
