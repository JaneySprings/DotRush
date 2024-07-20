using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Workspaces.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.CodeAnalysis;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Services;

public class CodeAnalysisService {
    private const int AnalysisFrequencyMs = 1000;

    private readonly ILanguageServerFacade? serverFacade;
    private readonly WorkspaceService workspaceService;
    private CancellationTokenSource compilationTokenSource;

    public CompilationHost CompilationHost { get; init; }
    public CodeActionHost CodeActionHost { get; init; }

    public CodeAnalysisService(ILanguageServerFacade? serverFacade, WorkspaceService workspaceService) {
        this.workspaceService = workspaceService;
        this.serverFacade = serverFacade;

        compilationTokenSource = new CancellationTokenSource();
        CodeActionHost = new CodeActionHost();
        CompilationHost = new CompilationHost();
        CompilationHost.DiagnosticsChanged += OnDiagnosticsCollectionChanged;
    }
    public bool HasDiagnosticsForFilePath(string filePath) {
        return CompilationHost.GetDiagnostics(filePath) != null;
    }
    public Task PublishDiagnosticsAsync(string filePath) {
        if (workspaceService.Solution == null)
            return Task.CompletedTask;

        ResetCancellationToken();
        var cancellationToken = compilationTokenSource.Token;
        var projects = workspaceService.Solution
            .GetProjectIdsWithFilePath(filePath)
            .Select(workspaceService.Solution.GetProject);

        return SafeExtensions.InvokeAsync(async () => {
            await Task.Delay(AnalysisFrequencyMs, cancellationToken).ConfigureAwait(false);
            await CompilationHost.DiagnoseAsync(projects, cancellationToken).ConfigureAwait(false);
        });
    }

    private void OnDiagnosticsCollectionChanged(object? sender, DiagnosticsCollectionChangedEventArgs e) {
        serverFacade?.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams() {
            Diagnostics = new Container<ProtocolModels.Diagnostic>(e.Diagnostics.Select(d => d.ToServerDiagnostic())),
            Uri = DocumentUri.FromFileSystemPath(e.FilePath),
        });
    }
    private void ResetCancellationToken() {
        compilationTokenSource?.Cancel();
        compilationTokenSource?.Dispose();
        compilationTokenSource = new CancellationTokenSource();
    }
}