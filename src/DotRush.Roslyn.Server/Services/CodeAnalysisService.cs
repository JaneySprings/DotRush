using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Workspaces.Extensions;
using DotRush.Roslyn.Server.Extensions;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using DotRush.Roslyn.CodeAnalysis;

namespace DotRush.Roslyn.Server.Services;

public class CodeAnalysisService {
    private const int AnalysisFrequencyMs = 500;

    private readonly ILanguageServerFacade? serverFacade;
    private readonly WorkspaceService workspaceService;
    private readonly IConfigurationService configurationService;
    private CancellationTokenSource compilationTokenSource;

    public CompilationHost CompilationHost { get; }
    public CodeActionHost CodeActionHost { get; }

    public CodeAnalysisService(ILanguageServerFacade? serverFacade, WorkspaceService workspaceService, IConfigurationService configurationService) {
        this.configurationService = configurationService;
        this.workspaceService = workspaceService;
        this.serverFacade = serverFacade;

        CompilationHost = new CompilationHost();
        CodeActionHost = new CodeActionHost();
        compilationTokenSource = new CancellationTokenSource();

        CompilationHost.DiagnosticsChanged += OnDiagnosticsCollectionChanged;
    }

    public Task PublishDiagnosticsAsync() {
        if (workspaceService.Solution == null)
            return Task.CompletedTask;

        ResetCancellationToken();
        var cancellationToken = compilationTokenSource.Token;
        return SafeExtensions.InvokeAsync(async () => {
            await Task.Delay(AnalysisFrequencyMs, cancellationToken).ConfigureAwait(false);
            await CompilationHost.DiagnoseAsync(workspaceService.Solution, configurationService.UseRoslynAnalyzers, cancellationToken).ConfigureAwait(false);
        });
    }

    private void OnDiagnosticsCollectionChanged(object? sender, DiagnosticsCollectionChangedEventArgs e) {
        CurrentSessionLogger.Debug($"Publishing {e.Diagnostics.Count} diagnostics for document: {e.DocumentPath}");
        serverFacade?.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams() {
            Diagnostics = new Container<ProtocolModels.Diagnostic>(e.Diagnostics.Select(d => d.ToServerDiagnostic())),
            Uri = DocumentUri.FromFileSystemPath(e.DocumentPath),
        });
    }
    private void ResetCancellationToken() {
        compilationTokenSource.Cancel();
        compilationTokenSource?.Dispose();
        compilationTokenSource = new CancellationTokenSource();
    }
}