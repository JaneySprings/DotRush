using System.Collections.ObjectModel;
using DotRush.Roslyn.Common.Extensions;
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
    public Task LoadSolutionAsync(CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(workspace);
        return LoadSolutionAsync(workspace, cancellationToken);
    }
    public void FindTargetsInWorkspace(IEnumerable<string>? workspaceFolders, IEnumerable<string>? excludePatterns = null) {
        if (workspaceFolders == null)
            return;

        foreach (var workspaceFolder in workspaceFolders) {
            var directoryProjectFiles = FileSystemExtensions.GetVisibleFiles(workspaceFolder, LanguageExtensions.IsProjectFile);
            if (directoryProjectFiles.Any()) {
                AddProjectFiles(directoryProjectFiles, excludePatterns);
                continue;
            }
            FindTargetsInWorkspace(FileSystemExtensions.GetVisibleDirectories(workspaceFolder), excludePatterns);
        }
    }
    public void AddTargets(IEnumerable<string> projectFiles) {
        AddProjectFiles(projectFiles);
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
