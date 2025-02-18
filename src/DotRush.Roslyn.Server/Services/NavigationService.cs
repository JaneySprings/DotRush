using DotRush.Common.Extensions;
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
        return SafeExtensions.InvokeAsync(default(string), async () => {
            return await navigationHost.EmitDecompiledFileAsync(symbol, project, cancellationToken).ConfigureAwait(false);
        });
    }
    public Task<string?> EmitCompilerGeneratedFileAsync(Location location, Project project, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync(default(string), async () => {
            return await navigationHost.EmitCompilerGeneratedFileAsync(location, project, cancellationToken).ConfigureAwait(false);
        });
    }
    public void UpdateSolution(Solution? solution) {
        navigationHost.Solution = solution;
    }
}