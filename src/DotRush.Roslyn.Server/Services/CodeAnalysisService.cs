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
        if (!documents.Any())
            return new ReadOnlyCollection<DiagnosticContext>(new List<DiagnosticContext>());

        var documentFilePath = documents.First().FilePath;
        var diagnostics = await compilationHost.DiagnoseAsync(documents, configurationService.EnableAnalyzers, cancellationToken).ConfigureAwait(false);
        return diagnostics.Where(d => !d.Diagnostic.IsHiddenInUI() && PathExtensions.Equals(d.FilePath, documentFilePath)).ToList().AsReadOnly();
    }
    public ReadOnlyCollection<DiagnosticContext> GetDiagnostics() {
        var diagnostics = compilationHost.GetDiagnostics();
        return diagnostics.Where(d => !d.Diagnostic.IsHiddenInUI()).ToList().AsReadOnly();
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