using DotRush.Roslyn.Navigation;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Server.Services;

public class NavigationService {
    private readonly NavigationHost navigationHost;
    public Solution? Solution => navigationHost.Solution;

    public NavigationService() {
        navigationHost = new NavigationHost();
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
    public void UpdateSolution(Solution? solution) {
        navigationHost.UpdateSolution(solution);
    }
    public void ClearCache() {
        // We can't use CloseDocument(string) because vscode can close and reopen decompiled file
        navigationHost.ClearCache();
    }
}
