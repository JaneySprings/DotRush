using System.Collections.Immutable;
using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.Options;
using EmmyLua.LanguageServer.Framework.Protocol.Message.CodeAction;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using CodeAnalysisCodeAction = Microsoft.CodeAnalysis.CodeActions.CodeAction;

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

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.CodeActionProvider = new CodeActionOptions {
            CodeActionKinds = new List<CodeActionKind> { CodeActionKind.QuickFix },
            ResolveProvider = true
        };
    }
    protected override Task<CodeActionResponse> Handle(CodeActionParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(new CodeActionResponse(new List<CommandOrCodeAction>()), async () => {
            codeActionsCache.Clear();

            var filePath = request.TextDocument.Uri.FileSystemPath;
            var serverDiagnostics = request.Context.Diagnostics?.Where(it => it.Data?.Value != null);
            var diagnosticIds = serverDiagnostics?.Select(it => (int)it.Data!.Value!);

            return diagnosticIds == null || !diagnosticIds.Any()
                ? await GetRefactoringsAsync(filePath, token).ConfigureAwait(false)
                : await GetQuickFixesAsync(filePath, diagnosticIds, token).ConfigureAwait(false);
        });
    }
    protected override Task<CodeAction> Resolve(CodeAction request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(request, async () => {
            if (request.Data?.Value == null || workspaceService.Solution == null) {
                currentClassLogger.Error($"CodeAction '{request.Title}' data is null or solution is null");
                return request;
            }

            var codeActionId = (int)request.Data.Value;
            if (!codeActionsCache.TryGetValue(codeActionId, out var codeAction)) {
                currentClassLogger.Error($"CodeAction '{request.Title}' with id '{codeActionId}' not found");
                return request;
            }

            var result = await codeAction.ResolveCodeActionAsync(workspaceService.Solution, token).ConfigureAwait(false);
            if (result == null) {
                currentClassLogger.Error($"CodeAction '{request.Title}' with id '{codeActionId}' failed to resolve");
                return request;
            }

            return result;
        });
    }

    private async Task<CodeActionResponse> GetQuickFixesAsync(string filePath, IEnumerable<int> diagnosticIds, CancellationToken cancellationToken) {
        var result = new List<CommandOrCodeAction>();
        var diagnosticContexts = diagnosticIds
            .Select(id => codeAnalysisService.CompilationHost.GetDiagnosticContextById(id))
            .Where(it => it != null)
            .GroupBy(it => it!.Diagnostic.Id);

        foreach (var group in diagnosticContexts) {
            var project = group.FirstOrDefault()?.RelatedProject;
            if (project == null) {
                currentClassLogger.Debug($"Project not found for diagnostic id '{group.Key}'");
                continue;
            }

            var codeFixProviders = codeAnalysisService.CodeActionHost.GetCodeFixProvidersForDiagnosticId(group.Key, project);
            if (codeFixProviders == null) {
                currentClassLogger.Debug($"CodeFixProviders not found for diagnostic id '{group.Key}'");
                return new CodeActionResponse(result);
            }

            var document = project.Documents.FirstOrDefault(it => PathExtensions.Equals(it.FilePath, filePath));
            if (document == null) {
                currentClassLogger.Debug($"Document not found for file path '{filePath}'");
                return new CodeActionResponse(result);
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
                            result.Add(new CommandOrCodeAction(singleCodeAction.ToCodeAction(CodeActionKind.QuickFix)));
                    }
                }, cancellationToken)).ConfigureAwait(false);
            }
        }

        return new CodeActionResponse(result);
    }

    //TODO
    #pragma warning disable CS1998 
    #pragma warning disable CA1822
    private async Task<CodeActionResponse> GetRefactoringsAsync(string filePath, CancellationToken cancellationToken) {
        return new CodeActionResponse(new List<CommandOrCodeAction>());
    }
    #pragma warning restore CS1998
    #pragma warning restore CA1822
}