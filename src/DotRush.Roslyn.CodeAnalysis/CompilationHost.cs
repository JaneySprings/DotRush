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

    public CompilationHost() {
        currentClassLogger = new CurrentClassLogger(nameof(CompilationHost));
        diagnosticAnalyzersLoader = new DiagnosticAnalyzersLoader();
        workspaceDiagnostics = new DiagnosticCollection();
    }

    public ReadOnlyCollection<DiagnosticContext> GetDiagnostics() {
        return workspaceDiagnostics.GetDiagnostics();
    }
    public ReadOnlyCollection<DiagnosticContext> GetDiagnosticsByDocumentSpan(Document document, TextSpan span) {
        return workspaceDiagnostics.GetDiagnosticsByDocumentSpan(document, span);
    }
    public string GetCollectionToken() {
        return workspaceDiagnostics.GetCollectionToken();
    }

    public async Task<ReadOnlyCollection<DiagnosticContext>> DiagnoseAsync(IEnumerable<Document> documents, bool enableAnalyzers, CancellationToken cancellationToken) {
        documents.ForEach(document => workspaceDiagnostics.ClearDiagnostics(document));

        bool hasAnalyzersDiagnose = false;
        foreach (var document in documents) {
            currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Diagnostics for {document.Project.Name} started");

            var compilation = enableAnalyzers 
                ? await DiagnoseWithSuppressorsAsync(document.Project, cancellationToken).ConfigureAwait(false)
                : await DiagnoseAsync(document.Project, cancellationToken).ConfigureAwait(false);

            if (compilation != null && enableAnalyzers && !hasAnalyzersDiagnose) {
                // Diagnose with analyzers only once for the first tfm (I think it is enough)
                await AnalyzerDiagnoseAsync(document, compilation, cancellationToken).ConfigureAwait(false);
                hasAnalyzersDiagnose = true;
            }

            currentClassLogger.Debug($"[{cancellationToken.GetHashCode()}]: Diagnostics for {document.Project.Name} finished");
        }

        return workspaceDiagnostics.GetDiagnostics();
    }

    private async Task<Compilation?> DiagnoseAsync(Project project, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested)
            return null;
        
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null)
            return null;

        var parseDiagnostics = compilation.GetParseDiagnostics(cancellationToken);
        workspaceDiagnostics.AddDiagnostics(project.Id, parseDiagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project)));

        var declarationDiagnostics = compilation.GetDeclarationDiagnostics(cancellationToken);
        workspaceDiagnostics.AddDiagnostics(project.Id, declarationDiagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project)));

        var methodBodyDiagnostics = compilation.GetMethodBodyDiagnostics(cancellationToken);
        workspaceDiagnostics.AddDiagnostics(project.Id, methodBodyDiagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project)));

        return compilation;
    }
    private async Task<Compilation?> DiagnoseWithSuppressorsAsync(Project project, CancellationToken cancellationToken) {
         if (cancellationToken.IsCancellationRequested)
            return null;
        
        var diagnosticSuppressors = diagnosticAnalyzersLoader.GetSuppressors(project);
        if (diagnosticSuppressors == null || diagnosticSuppressors.Length == 0)
            return await DiagnoseAsync(project, cancellationToken);
        
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null)
            return null;

        var compilationWithSuppressors = compilation.WithAnalyzers(diagnosticSuppressors, project.AnalyzerOptions);
        if (compilationWithSuppressors == null)
            return null;

        var diagnostics = await compilationWithSuppressors.GetAllDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        workspaceDiagnostics.AddDiagnostics(project.Id, diagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project)));
        return compilation;
    }
    private async Task AnalyzerDiagnoseAsync(Document document, Compilation compilation, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested)
            return;
        
        var project = document.Project;
        var diagnosticAnalyzers = diagnosticAnalyzersLoader.GetComponents(project);
        if (diagnosticAnalyzers == null || diagnosticAnalyzers.Length == 0)
            return;

        var compilationWithAnalyzers = compilation.WithAnalyzers(diagnosticAnalyzers, project.AnalyzerOptions);
        if (compilationWithAnalyzers == null)
            return;

        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        if (syntaxTree == null)
            return;
        var syntaxDiagnostics = await compilationWithAnalyzers.GetAnalyzerSyntaxDiagnosticsAsync(syntaxTree, cancellationToken).ConfigureAwait(false);
        workspaceDiagnostics.AddDiagnostics(project.Id, syntaxDiagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project, true)));
        
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel == null)
            return;
        var semanticDiagnostics = await compilationWithAnalyzers.GetAnalyzerSemanticDiagnosticsAsync(semanticModel, null, cancellationToken).ConfigureAwait(false);
        workspaceDiagnostics.AddDiagnostics(project.Id, semanticDiagnostics.Select(diagnostic => new DiagnosticContext(diagnostic, project, true)));
    }
}
