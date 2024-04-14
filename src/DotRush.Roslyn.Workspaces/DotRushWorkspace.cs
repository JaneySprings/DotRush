using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common.Logging;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotRush.Roslyn.Workspaces;

public abstract class DotRushWorkspace : SolutionController {
    private MSBuildWorkspace? workspace;

    protected abstract Dictionary<string, string> WorkspaceProperties { get; }
    protected abstract bool LoadMetadataForReferencedProjects { get; }
    protected abstract bool SkipUnrecognizedProjects { get; }

    public void InitializeWorkspace(Action<Exception>? errorHandler = null) {
        TryRegisterDotNetEnvironment(errorHandler);
        workspace = MSBuildWorkspace.Create(WorkspaceProperties);
        workspace.LoadMetadataForReferencedProjects = LoadMetadataForReferencedProjects;
        workspace.SkipUnrecognizedProjects = SkipUnrecognizedProjects;
    }
    public async Task LoadSolutionAsync(CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(workspace);
        await LoadSolutionAsync(workspace, cancellationToken);
    }
    public void FindTargetsInWorkspace(IEnumerable<string>? workspaceFolders) {
        if (workspaceFolders == null)
            return;

        foreach (var workspaceFolder in workspaceFolders) {
            var directoryProjectFiles = FileSystemExtensions.GetVisibleFiles(workspaceFolder, LanguageExtensions.IsProjectFile);
            if (directoryProjectFiles.Any()) {
                AddProjectFiles(directoryProjectFiles);
                continue;
            }
            FindTargetsInWorkspace(FileSystemExtensions.GetVisibleDirectories(workspaceFolder));
        }
    }
    public void AddTargets(IEnumerable<string> projectFiles) {
        AddProjectFiles(projectFiles);
    }

    private static bool TryRegisterDotNetEnvironment(Action<Exception>? errorHandler) {
        try {
            if (MSBuildLocator.CanRegister && !MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();
            return true;
        } catch (Exception e) {
            CurrentSessionLogger.Error(e);
            errorHandler?.Invoke(e);
            return false;
        }
    }
}
