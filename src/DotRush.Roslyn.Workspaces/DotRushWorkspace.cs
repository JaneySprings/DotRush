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
    
    public bool TryInitializeWorkspace(IEnumerable<string>? projects, Action<Exception>? errorHandler = null) {
        if (!TryRegisterDotNetEnvironment(errorHandler))
            return false;

        workspace = MSBuildWorkspace.Create(WorkspaceProperties);
        workspace.LoadMetadataForReferencedProjects = LoadMetadataForReferencedProjects;
        workspace.SkipUnrecognizedProjects = SkipUnrecognizedProjects;
        
        if (projects != null)
            AddProjectFiles(projects);
        
        return true;
    }
    public async Task LoadSolutionAsync(CancellationToken cancellationToken) {
        if (workspace == null)
            throw new InvalidOperationException($"Workspace is not initialized. Call {nameof(TryInitializeWorkspace)} method.");
        
        await LoadSolutionAsync(workspace, cancellationToken);
    }
    public void AddProjectFilesFromFolders(IEnumerable<string>? workspaceFolders) {
        ClearAllProjects();
        if (workspaceFolders == null)
            return;

        foreach (var workspaceFolder in workspaceFolders) {
            var directoryProjectFiles = FileSystemExtensions.GetVisibleFiles(workspaceFolder).Where(it => ProjectsController.IsProjectFile(it));
            if (directoryProjectFiles.Any()) {
                AddProjectFiles(directoryProjectFiles);
                continue;
            }
            AddProjectFiles(FileSystemExtensions.GetVisibleDirectories(workspaceFolder));
        }
    }

    private static bool TryRegisterDotNetEnvironment(Action<Exception>? errorHandler) {
        try {
            MSBuildLocator.RegisterDefaults();
            return true;
        } catch (Exception e) {
            CurrentSessionLogger.Error(e);
            errorHandler?.Invoke(e);
            return false;
        }
    }
}
