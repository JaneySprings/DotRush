using DotRush.Roslyn.CodeAnalysis.Components;
using DotRush.Roslyn.Navigation;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Server.Services;

public class NavigationService : IClearable {
    private readonly NavigationHost navigationHost;
    private readonly WorkspaceService workspaceService;

    public Solution? HostSolution => navigationHost.Solution;
    public Solution? OriginalSolution => workspaceService.Solution;

    public NavigationService(WorkspaceService workspaceService) {
        this.workspaceService = workspaceService;
        this.navigationHost = new NavigationHost();

        workspaceService.WorkspaceStateChanged += (_, _) => navigationHost.UpdateSolution(workspaceService.Solution);
    }

    public Task<List<FileLinePositionSpan>> FindDefinitionsAsync(ISymbol symbol, Project project, CancellationToken cancellationToken) {
        return navigationHost.FindDefinitionsAsync(symbol, project, cancellationToken);
    }
    public Task<List<FileLinePositionSpan>> FindReferencesAsync(ISymbol symbol, CancellationToken cancellationToken) {
        return navigationHost.FindReferencesAsync(symbol, cancellationToken);
    }
    public Task<FileLinePositionSpan?> EmitCompilerGeneratedFileAsync(Location location, Project project, CancellationToken cancellationToken) {
        return navigationHost.EmitCompilerGeneratedFileAsync(location, project, cancellationToken);
    }
    public Solution? GetRequiredSolution(string documentFilePath) {
        if (navigationHost.IsAttachedToHost(documentFilePath))
            return navigationHost.Solution;
        return workspaceService.Solution;
    }
    public void ClearCache() {
        // We can't use CloseDocument(string) because vscode can close and reopen decompiled file
        navigationHost.ClearCache();
    }
}
