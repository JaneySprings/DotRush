using System.Collections.ObjectModel;
using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Components;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis;

public class CompilationHost {
    private readonly DiagnosticAnalyzersLoader diagnosticAnalyzersLoader;
    private readonly DiagnosticCollection workspaceDiagnostics;
    private readonly CurrentClassLogger currentClassLogger;

    public CompilationHost(IAdditionalComponentsProvider additionalComponentsProvider) {
        currentClassLogger = new CurrentClassLogger(nameof(CompilationHost));
        diagnosticAnalyzersLoader = new DiagnosticAnalyzersLoader(additionalComponentsProvider);
        workspaceDiagnostics = new DiagnosticCollection();
    }

    public ReadOnlyDictionary<string, List<DiagnosticContext>> GetDiagnostics() {
        return workspaceDiagnostics.GetDiagnostics();
    }
    public ReadOnlyCollection<DiagnosticContext> GetDiagnosticsByDocumentSpan(Document document, TextSpan span) {
        return workspaceDiagnostics.GetDiagnosticsByDocumentSpan(document, span);
    }
    public string GetCollectionToken() {
        return workspaceDiagnostics.GetCollectionToken();
    }

    public async Task<ReadOnlyDictionary<string, List<DiagnosticContext>>> DiagnoseDocumentsAsync(IEnumerable<Document> documents, bool enableAnalyzers, CancellationToken cancellationToken) {
        documents.ForEach(document => workspaceDiagnostics.ClearDocumentDiagnostics(document));

        bool hasAnalyzersDiagnose = false;
        foreach (var document in documents) {
            currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Diagnostics for {document.Name} started");
            // No way to use Suppressors for single document
            await DiagnoseAsync(document, cancellationToken).ConfigureAwait(false);

            if (enableAnalyzers && !hasAnalyzersDiagnose) {
                // Diagnose with analyzers only once for the first tfm (I think it is enough)
                await AnalyzerDiagnoseAsync(document, cancellationToken).ConfigureAwait(false);
                hasAnalyzersDiagnose = true;
            }

            currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Diagnostics for {document.Name} finished");
        }

        return workspaceDiagnostics.GetDiagnostics();
    }
    public async Task<ReadOnlyDictionary<string, List<DiagnosticContext>>> DiagnoseProjectsAsync(IEnumerable<Document> documents, bool enableAnalyzers, CancellationToken cancellationToken) {
        documents.ForEach(document => workspaceDiagnostics.ClearProjectDiagnostics(document));

        bool hasAnalyzersDiagnose = false;
        foreach (var document in documents) {
            currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Diagnostics for {document.Project.Name} started");

            if (enableAnalyzers) 
                await DiagnoseWithSuppressorsAsync(document.Project, cancellationToken).ConfigureAwait(false);
            else 
                await DiagnoseAsync(document.Project, cancellationToken).ConfigureAwait(false);

            if (enableAnalyzers && !hasAnalyzersDiagnose) {
                // Diagnose with analyzers only once for the first tfm (I think it is enough)
                await AnalyzerDiagnoseAsync(document, cancellationToken).ConfigureAwait(false);
                hasAnalyzersDiagnose = true;
            }

            currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Diagnostics for {document.Project.Name} finished");
        }

        return workspaceDiagnostics.GetDiagnostics();
    }

    #region DiagnosticProcessing
    private async Task DiagnoseAsync(Project project, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested)
            return;
        
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null)
            return;

        var diagnostics = compilation.GetDiagnostics(cancellationToken);
        workspaceDiagnostics.AddDiagnostics(project.Id, diagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project)));
    }
    private async Task DiagnoseAsync(Document document, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested)
            return;
        
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel == null)
            return;

        var diagnostics = semanticModel.GetDiagnostics(null, cancellationToken);
        workspaceDiagnostics.AddDiagnostics(document.Project.Id, diagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, document.Project)));
    }
    private async Task DiagnoseWithSuppressorsAsync(Project project, CancellationToken cancellationToken) {
         if (cancellationToken.IsCancellationRequested)
            return;
        
        var diagnosticSuppressors = diagnosticAnalyzersLoader.GetSuppressors(project);
        if (diagnosticSuppressors == null || diagnosticSuppressors.Length == 0) {
            await DiagnoseAsync(project, cancellationToken);
            return;
        }
        
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        var compilationWithSuppressors = compilation?.WithAnalyzers(diagnosticSuppressors, project.AnalyzerOptions);
        if (compilationWithSuppressors == null)
            return;

        var diagnostics = await compilationWithSuppressors.GetAllDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        workspaceDiagnostics.AddDiagnostics(project.Id, diagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project)));
    }
    private async Task AnalyzerDiagnoseAsync(Document document, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested)
            return;
        
        var project = document.Project;
        var diagnosticAnalyzers = diagnosticAnalyzersLoader.GetComponents(project);
        if (diagnosticAnalyzers == null || diagnosticAnalyzers.Length == 0)
            return;

        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel == null || syntaxTree == null)
            return;
    
        var compilationWithAnalyzers = semanticModel.Compilation.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions);
        if (compilationWithAnalyzers == null)
            return;

        var syntaxDiagnostics = await compilationWithAnalyzers.GetAnalyzerSyntaxDiagnosticsAsync(syntaxTree, cancellationToken).ConfigureAwait(false);
        workspaceDiagnostics.AddDiagnostics(project.Id, syntaxDiagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project, true)));
        
        var semanticDiagnostics = await compilationWithAnalyzers.GetAnalyzerSemanticDiagnosticsAsync(semanticModel, null, cancellationToken).ConfigureAwait(false);
        workspaceDiagnostics.AddDiagnostics(project.Id, semanticDiagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project, true)));
    }
    #endregion
}
