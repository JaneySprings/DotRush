using System.Collections.ObjectModel;
using DotRush.Roslyn.CodeAnalysis;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace DotRush.Roslyn.Server.Services;

public class CodeAnalysisService {
    private readonly ConfigurationService configurationService;
    private readonly CodeActionHost codeActionHost;
    private readonly CompilationHost compilationHost;

    public CodeAnalysisService(ConfigurationService configurationService) {
        this.configurationService = configurationService;
        this.codeActionHost = new CodeActionHost();
        this.compilationHost = new CompilationHost();
    }

    public Task<ReadOnlyCollection<DiagnosticContext>> DiagnoseAsync(IEnumerable<ProjectId> projectIds, Solution solution, CancellationToken cancellationToken) {
        return compilationHost.DiagnoseAsync(projectIds, solution, configurationService.EnableAnalyzers, cancellationToken);
    }
    public DiagnosticContext? GetDiagnosticContextById(int diagnosticId) {
        return compilationHost.GetDiagnosticContextById(diagnosticId);
    }
    public string GetDiagnosticsCollectionToken() {
        return compilationHost.GetCollectionToken();
    }
    public ReadOnlyCollection<DiagnosticContext> GetDiagnostics() {
        return compilationHost.GetDiagnostics();
    }

    public IEnumerable<CodeFixProvider>? GetCodeFixProvidersForDiagnosticId(string? diagnosticId, Project? project) {
        return codeActionHost.GetCodeFixProvidersForDiagnosticId(diagnosticId, project);
    }
    public IEnumerable<CodeRefactoringProvider>? GetCodeRefactoringProvidersForProject(Project? project) {
        if (!configurationService.EnableAnalyzers)
            return null;

        return codeActionHost.GetCodeRefactoringProvidersForProject(project);
    }
}