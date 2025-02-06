using Microsoft.CodeAnalysis;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Workspaces.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.CodeAnalysis;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Client.PublishDiagnostics;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Model;

namespace DotRush.Roslyn.Server.Services;

public class CodeAnalysisService {
    private const int AnalysisFrequencyMs = 500;

    private readonly ConfigurationService configurationService;
    private readonly WorkspaceService workspaceService;
    private CancellationTokenSource compilationTokenSource;

    public CompilationHost CompilationHost { get; init; }
    public CodeActionHost CodeActionHost { get; init; }

    public CodeAnalysisService(ConfigurationService configurationService, WorkspaceService workspaceService) {
        this.configurationService = configurationService;
        this.workspaceService = workspaceService;

        compilationTokenSource = new CancellationTokenSource();
        CodeActionHost = new CodeActionHost();
        CompilationHost = new CompilationHost();
        CompilationHost.DiagnosticsChanged += OnDiagnosticsCollectionChanged;
    }

    public Task PublishDiagnosticsAsync(string documentPath) {
        ResetCancellationToken();
        if (workspaceService.Solution == null)
            return Task.CompletedTask;

        var cancellationToken = compilationTokenSource.Token;
        var projectIdsByDocument = workspaceService.Solution.GetProjectIdsWithDocumentFilePath(documentPath);
        var projectIdsByAdditionalDocument = workspaceService.Solution.GetProjectIdsWithAdditionalDocumentFilePath(documentPath);
        var projectIds = projectIdsByDocument.Concat(projectIdsByAdditionalDocument).Distinct();
        var projects = projectIds.Select(workspaceService.Solution.GetProject).Where(p => p != null);

        if (projects == null || !projects.Any())
            return Task.CompletedTask;
        
        if (!configurationService.UseMultitargetDiagnostics)
            projects = projects.Take(1);

        ResetClientDiagnostics(projects!);
        return SafeExtensions.InvokeAsync(async () => {
            await Task.Delay(AnalysisFrequencyMs, cancellationToken).ConfigureAwait(false);
            await CompilationHost.DiagnoseAsync(projects!, cancellationToken).ConfigureAwait(false);
        });
    }
    public bool HasDiagnostics(string documentPath) {
        var diagnostics = CompilationHost.GetDiagnostics(documentPath);
        return diagnostics != null && diagnostics.Any();
    }
    public void ResetClientDiagnostics(string documentPath) {
        _ = LanguageServer.Proxy.PublishDiagnostics(new PublishDiagnosticsParams() {
            Diagnostics = new List<ProtocolModels.Diagnostic.Diagnostic>(),
            Uri = documentPath,
        });
    }

    private void OnDiagnosticsCollectionChanged(object? sender, DiagnosticsCollectionChangedEventArgs e) {
        _ = LanguageServer.Proxy.PublishDiagnostics(new PublishDiagnosticsParams() {
            Diagnostics = e.Diagnostics.Select(d => d.ToServerDiagnostic()).ToList(),
            Uri = e.FilePath,
        });
    }
    private void ResetClientDiagnostics(IEnumerable<Project> projects) {
        var documentPaths = projects.SelectMany(p => p.Documents).Select(d => d.FilePath).Distinct().Where(p => p != null);
        foreach (var documentPath in documentPaths)
            ResetClientDiagnostics(documentPath!);
    }
    private void ResetCancellationToken() {
        compilationTokenSource?.Cancel();
        compilationTokenSource?.Dispose();
        compilationTokenSource = new CancellationTokenSource();
    }
}