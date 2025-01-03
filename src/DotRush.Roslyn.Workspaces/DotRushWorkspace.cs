using System.Collections.ObjectModel;
using DotRush.Roslyn.Common.Logging;
using Microsoft.Build.Locator;
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
    public async Task LoadAsync(IEnumerable<string> targets, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(workspace);
        
        var solutionFiles = targets.Where(it => Path.GetExtension(it).Equals(".sln", StringComparison.OrdinalIgnoreCase));
        var projectFiles = targets.Where(it => Path.GetExtension(it).Equals(".csproj", StringComparison.OrdinalIgnoreCase));

        if (solutionFiles.Any())
            await LoadSolutionAsync(workspace, solutionFiles, cancellationToken);
        if (projectFiles.Any())
            await LoadProjectsAsync(workspace, projectFiles, cancellationToken);
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
