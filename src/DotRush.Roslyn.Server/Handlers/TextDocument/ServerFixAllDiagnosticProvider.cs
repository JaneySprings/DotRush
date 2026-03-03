using DotRush.Roslyn.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class ServerFixAllDiagnosticProvider : FixAllContext.DiagnosticProvider {
    private readonly CodeAnalysisService codeAnalysisService;

    public ServerFixAllDiagnosticProvider(CodeAnalysisService codeAnalysisService) {
        this.codeAnalysisService = codeAnalysisService;
    }

    public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) {
        var diagnostics = codeAnalysisService.GetDiagnosticsByProject(project);
        return Task.FromResult(diagnostics.Select(d => d.Diagnostic));
    }

    public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken) {
        var diagnostics = codeAnalysisService.GetDiagnosticsByDocument(document);
        return Task.FromResult(diagnostics.Select(d => d.Diagnostic));
    }

    public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) {
        // Project-level diagnostics without location. Not currently cached separately in workspaceDiagnostics,
        // so we return empty. GetAllDiagnosticsAsync is what FixAll uses for the actual project scope.
        return Task.FromResult(Enumerable.Empty<Diagnostic>());
    }
}
