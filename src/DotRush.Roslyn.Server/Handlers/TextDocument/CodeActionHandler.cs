using System.Collections.Immutable;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using CodeAnalysisCodeAction = Microsoft.CodeAnalysis.CodeActions.CodeAction;
using FileSystemExtensions = DotRush.Roslyn.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class CodeActionHandler : CodeActionHandlerBase {
    private readonly WorkspaceService workspaceService;
    private readonly CodeAnalysisService codeAnalysisService;
    private readonly Dictionary<int, CodeAnalysisCodeAction> codeActionsCache;
    private readonly CurrentClassLogger currentClassLogger;

    public CodeActionHandler(WorkspaceService workspaceService, CodeAnalysisService codeAnalysisService) {
        codeActionsCache = new Dictionary<int, CodeAnalysisCodeAction>();
        currentClassLogger = new CurrentClassLogger(nameof(CodeActionHandler));
        this.workspaceService = workspaceService;
        this.codeAnalysisService = codeAnalysisService;
    }

    protected override CodeActionRegistrationOptions CreateRegistrationOptions(CodeActionCapability capability, ClientCapabilities clientCapabilities) {
        return new CodeActionRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments,
            CodeActionKinds = new Container<CodeActionKind>(CodeActionKind.QuickFix),
            ResolveProvider = true,
        };
    }
    public override Task<CommandOrCodeActionContainer?> Handle(CodeActionParams request, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync(async () => {
            codeActionsCache.Clear();

            var filePath = request.TextDocument.Uri.GetFileSystemPath();
            var serverDiagnostics = request.Context.Diagnostics?.Where(it => it.Data?.ToObject<int>() != null);
            var diagnosticIds = serverDiagnostics?.Select(it => it.Data!.ToObject<int>());

            return diagnosticIds == null || !diagnosticIds.Any()
                ? await GetRefactoringsAsync(filePath, cancellationToken).ConfigureAwait(false)
                : await GetQuickFixesAsync(filePath, diagnosticIds, cancellationToken).ConfigureAwait(false);
        });
    }
    public override Task<CodeAction> Handle(CodeAction request, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync(request, async () => {
            if (request.Data == null) {
                currentClassLogger.Error($"CodeAction '{request.Title}' data is null");
                return request;
            }

            var codeActionId = request.Data.ToObject<int>();
            if (!codeActionsCache.TryGetValue(codeActionId, out var codeAction)) {
                currentClassLogger.Error($"CodeAction '{request.Title}' with id '{codeActionId}' not found");
                return request;
            }

            var result = await codeAction.ResolveCodeActionAsync(workspaceService, cancellationToken).ConfigureAwait(false);
            if (result == null) {
                currentClassLogger.Error($"CodeAction '{request.Title}' with id '{codeActionId}' failed to resolve");
                return request;
            }

            return result;
        });
    }

    private async Task<CommandOrCodeActionContainer> GetQuickFixesAsync(string filePath, IEnumerable<int> diagnosticIds, CancellationToken cancellationToken) {
        var result = new List<CommandOrCodeAction>();
        var diagnosticHolderGroups = diagnosticIds
            .Select(id => codeAnalysisService.CompilationHost.GetDiagnosticById(filePath, id))
            .Where(it => it != null)
            .GroupBy(it => it!.Diagnostic.Id);

        foreach (var group in diagnosticHolderGroups) {
            var project = group.FirstOrDefault()?.Project;
            if (project == null) {
                currentClassLogger.Debug($"Project not found for diagnostic id '{group.Key}'");
                continue;
            }

            var codeFixProviders = codeAnalysisService.CodeActionHost.GetCodeFixProvidersForDiagnosticId(group.Key, project);
            if (codeFixProviders == null) {
                currentClassLogger.Debug($"CodeFixProviders not found for diagnostic id '{group.Key}'");
                return result;
            }

            var document = project.Documents.FirstOrDefault(it => FileSystemExtensions.PathEquals(it.FilePath, filePath));
            if (document == null) {
                currentClassLogger.Debug($"Document not found for file path '{filePath}'");
                return result;
            }

            foreach (var codeFixProvider in codeFixProviders) {
                var textSpan = group.Select(it => it!.Diagnostic.Location.SourceSpan).ToMergedTextSpan();
                var diagnostics = group.Select(it => it!.Diagnostic).ToImmutableArray();
                await codeFixProvider.RegisterCodeFixesAsync(new CodeFixContext(document, textSpan, diagnostics, (action, _) => {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    var singleCodeActions = action.ToSingleCodeActions().Where(x => !x.IsBlacklisted());
                    foreach (var singleCodeAction in singleCodeActions) {
                        if (codeActionsCache.TryAdd(singleCodeAction.GetUniqueId(), singleCodeAction))
                            result.Add(singleCodeAction.ToCodeAction(CodeActionKind.QuickFix));
                    }
                }, cancellationToken)).ConfigureAwait(false);
            }
        }

        return result;
    }
    private async Task<CommandOrCodeActionContainer> GetRefactoringsAsync(string filePath, CancellationToken cancellationToken) {
        return new CommandOrCodeActionContainer();
    }
}