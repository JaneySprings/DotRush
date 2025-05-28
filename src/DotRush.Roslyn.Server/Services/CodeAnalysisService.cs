using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis;
using DotRush.Roslyn.CodeAnalysis.Components;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.Server.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Client.PublishDiagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.Server.Services;

public class CodeAnalysisService : IAdditionalComponentsProvider {
    private readonly ConfigurationService configurationService;
    private readonly CodeActionHost codeActionHost;
    private readonly CompilationHost compilationHost;
    private readonly Thread workerThread;
    private readonly BlockingCollection<Func<Task>> workerTasks;

    public CodeAnalysisService(ConfigurationService configurationService) {
        this.configurationService = configurationService;
        this.codeActionHost = new CodeActionHost(this);
        this.compilationHost = new CompilationHost(this);
        this.workerTasks = new BlockingCollection<Func<Task>>();
        this.workerThread = new Thread(() => {
            foreach (var currentTask in workerTasks.GetConsumingEnumerable()) {
                Func<Task> latestTask = currentTask;
                // Drain queue and keep the most recent task
                while (workerTasks.TryTake(out var task))
                    latestTask = task;

                SafeExtensions.InvokeAsync(latestTask).Wait();
            }
        });
        this.workerThread.IsBackground = true;
    }

    public void StartWorkerThread() {
        workerThread.Start();
    }
    public void RequestDiagnosticsPublishing(IEnumerable<Document> documents) {
        if (!documents.Any())
            return;

        workerTasks.Add(async () => {
            await compilationHost.AnalyzeAsync(
                documents,
                configurationService.CompilerDiagnosticsScope,
                configurationService.AnalyzerDiagnosticsScope,
                CancellationToken.None
            ).ConfigureAwait(false);

            var diagnostics = compilationHost.GetDiagnostics();
            foreach (var pair in diagnostics) {
                await LanguageServer.Proxy.PublishDiagnostics(new PublishDiagnosticsParams {
                    Uri = pair.Key,
                    Diagnostics = pair.Value.Where(d => !d.IsHiddenInUI()).Select(d => d.ToServerDiagnostic()).ToList(),
                }).ConfigureAwait(false);
            }
        });
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