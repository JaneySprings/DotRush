using System.Collections.ObjectModel;
using DotRush.Roslyn.CodeAnalysis;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Server.Services;

public class CodeAnalysisService {
    private const int AnalysisFrequencyMs = 500;

    private readonly ConfigurationService configurationService;
    private CancellationTokenSource diagnosticTokenSource;

    public CompilationHost CompilationHost { get; init; }
    public CodeActionHost CodeActionHost { get; init; }

    public CodeAnalysisService(ConfigurationService configurationService) {
        this.configurationService = configurationService;
        this.diagnosticTokenSource = new CancellationTokenSource();
        CodeActionHost = new CodeActionHost();
        CompilationHost = new CompilationHost();
    }

    public async Task<ReadOnlyCollection<DiagnosticContext>> DiagnoseAsync(IEnumerable<ProjectId> projectIds, Solution solution) {
        var cancellationToken = GetToken();
        await Task.Delay(AnalysisFrequencyMs, cancellationToken).ConfigureAwait(false);
        return await CompilationHost.DiagnoseAsync(projectIds, solution, configurationService.EnableAnalyzers, cancellationToken).ConfigureAwait(false);
    }
    public void CancelPendingDiagnostics() {
        diagnosticTokenSource.Cancel();
        diagnosticTokenSource.Dispose();
        diagnosticTokenSource = new CancellationTokenSource();
    }


    private CancellationToken GetToken() {
        CancelPendingDiagnostics();
        return diagnosticTokenSource.Token;
    }
}