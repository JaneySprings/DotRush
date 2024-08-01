using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Navigation;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Server.Services;

public class NavigationService {
    private readonly NavigationHost navigationHost;
    public Solution? Solution => navigationHost.Solution;

    public NavigationService() {
        navigationHost = new NavigationHost();   
    }

    public Task<string?> EmitDecompiledFileAsync(ISymbol symbol, Project project, CancellationToken cancellationToken) {
        return SafeExtensions.Invoke<Task<string?>>(Task.FromResult<string?>(null), () => {
            return navigationHost.EmitDecompiledFileAsync(symbol, project, cancellationToken);
        });
    }
    public Task<string?> EmitCompilerGeneratedFileAsync(Location location, Project project, CancellationToken cancellationToken) {
        return SafeExtensions.Invoke<Task<string?>>(Task.FromResult<string?>(null), () => {
            return navigationHost.EmitCompilerGeneratedFileAsync(location, project, cancellationToken);
        });
    }
    public void UpdateSolution(Solution? solution) {
        navigationHost.Solution = solution;
    }
}