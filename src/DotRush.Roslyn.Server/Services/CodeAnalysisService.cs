using System.Collections.ObjectModel;
using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.Server.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

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

    public async Task<ReadOnlyCollection<DiagnosticContext>> GetDocumentDiagnostics(IEnumerable<Document> documents, CancellationToken cancellationToken) {
        var documentFilePath = documents.FirstOrDefault()?.FilePath;
        if (string.IsNullOrEmpty(documentFilePath))
            return new ReadOnlyCollection<DiagnosticContext>(new List<DiagnosticContext>());

        var diagnostics = configurationService.ProjectScopeDiagnostics
            ? await compilationHost.DiagnoseProjectsAsync(documents, configurationService.EnableAnalyzers, cancellationToken).ConfigureAwait(false)
            : await compilationHost.DiagnoseDocumentsAsync(documents, configurationService.EnableAnalyzers, cancellationToken).ConfigureAwait(false);
        
        if (!diagnostics.TryGetValue(documentFilePath, out List<DiagnosticContext>? value))
            return new ReadOnlyCollection<DiagnosticContext>(new List<DiagnosticContext>());

        return value.AsReadOnly();
    }
    public ReadOnlyDictionary<string, List<DiagnosticContext>> GetDiagnostics() {
        return compilationHost.GetDiagnostics();
    }

    public ReadOnlyCollection<DiagnosticContext> GetDiagnosticsByDocumentSpan(Document document, TextSpan span) {
        return compilationHost.GetDiagnosticsByDocumentSpan(document, span);
    }
    public string GetDiagnosticsCollectionToken() {
        return compilationHost.GetCollectionToken();
    }

    public IEnumerable<CodeFixProvider>? GetCodeFixProvidersForDiagnosticId(string? diagnosticId, Project? project) {
        return codeActionHost.GetCodeFixProvidersForDiagnosticId(diagnosticId, project);
    }
    public IEnumerable<CodeRefactoringProvider>? GetCodeRefactoringProvidersForProject(Project? project) {
        return codeActionHost.GetCodeRefactoringProvidersForProject(project);
    }
}