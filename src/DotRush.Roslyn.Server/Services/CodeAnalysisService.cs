using System.Collections.ObjectModel;
using DotRush.Roslyn.CodeAnalysis;
using DotRush.Roslyn.CodeAnalysis.Components;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.Server.Services;

public class CodeAnalysisService : IAdditionalComponentsProvider {
    private readonly ConfigurationService configurationService;
    private readonly CodeActionHost codeActionHost;
    private readonly CompilationHost compilationHost;

    public Guid DiagnosticsCollectionToken => compilationHost.DiagnosticsCollectionToken;
    private Guid previousSolutionToken;

    public CodeAnalysisService(ConfigurationService configurationService) {
        this.configurationService = configurationService;
        this.codeActionHost = new CodeActionHost(this);
        this.compilationHost = new CompilationHost(this);
    }

    public async Task<ReadOnlyCollection<DiagnosticContext>> GetDocumentDiagnosticsAsync(IEnumerable<Document> documents, Guid currentSolutionToken, CancellationToken cancellationToken) {
        var documentFilePath = documents.FirstOrDefault()?.FilePath;
        if (string.IsNullOrEmpty(documentFilePath))
            return new List<DiagnosticContext>().AsReadOnly();

        if (previousSolutionToken != currentSolutionToken) {
            await compilationHost.UpdateCompilerDiagnosticsAsync(documents, configurationService.CompilerDiagnosticsScope, cancellationToken).ConfigureAwait(false);
            await compilationHost.UpdateAnalyzerDiagnosticsAsync(documents, configurationService.AnalyzerDiagnosticsScope, cancellationToken).ConfigureAwait(false);
            previousSolutionToken = currentSolutionToken;
        }

        var diagnostics = compilationHost.GetDiagnostics();
        if (diagnostics.TryGetValue(documentFilePath, out List<DiagnosticContext>? value))
            return value.AsReadOnly();

        return new List<DiagnosticContext>().AsReadOnly();
    }
    public ReadOnlyDictionary<string, List<DiagnosticContext>> GetDiagnostics() {
        return compilationHost.GetDiagnostics();
    }
    public ReadOnlyCollection<DiagnosticContext> GetDiagnosticsByDocumentSpan(Document document, TextSpan span) {
        return compilationHost.GetDiagnosticsByDocumentSpan(document, span);
    }

    public IEnumerable<CodeFixProvider>? GetCodeFixProvidersForDiagnosticId(string? diagnosticId, Project project) {
        return codeActionHost.GetCodeFixProvidersForDiagnosticId(diagnosticId, project);
    }
    public IEnumerable<CodeRefactoringProvider>? GetCodeRefactoringProvidersForProject(Project project) {
        return codeActionHost.GetCodeRefactoringProvidersForProject(project);
    }

    bool IAdditionalComponentsProvider.IsEnabled {
        get => configurationService.AnalyzerDiagnosticsScope != AnalysisScope.None;
    }
    IEnumerable<string> IAdditionalComponentsProvider.GetAdditionalAssemblies() {
        return configurationService.AnalyzerAssemblies;
    }
}