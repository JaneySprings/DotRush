using System.Collections.ObjectModel;
using DotRush.Roslyn.CodeAnalysis;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Server.Services;

public class CodeAnalysisService {
    private readonly ConfigurationService configurationService;

    public CompilationHost CompilationHost { get; init; }
    public CodeActionHost CodeActionHost { get; init; }

    public CodeAnalysisService(ConfigurationService configurationService) {
        this.configurationService = configurationService;
        CodeActionHost = new CodeActionHost();
        CompilationHost = new CompilationHost();
    }

    public Task<ReadOnlyCollection<DiagnosticContext>> DiagnoseAsync(IEnumerable<ProjectId> projectIds, Solution solution, CancellationToken cancellationToken) {
        return CompilationHost.DiagnoseAsync(projectIds, solution, configurationService.EnableAnalyzers, cancellationToken);
    }
}