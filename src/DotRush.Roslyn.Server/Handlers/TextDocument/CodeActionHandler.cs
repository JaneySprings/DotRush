using System.Collections.Immutable;
using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.Options;
using EmmyLua.LanguageServer.Framework.Protocol.Message.CodeAction;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
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
            CodeActionKinds = new List<CodeActionKind> { CodeActionKind.QuickFix, CodeActionKind.Refactor },
            ResolveProvider = true
        };
    }
    protected override Task<CodeActionResponse> Handle(CodeActionParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(new CodeActionResponse(new List<CommandOrCodeAction>()), async () => {
            codeActionsCache.Clear();

            var result = new List<CommandOrCodeAction>();
            var filePath = request.TextDocument.Uri.FileSystemPath;
            result.AddRange(await GetQuickFixesAsync(filePath, request.Range, token).ConfigureAwait(false));
            result.AddRange(await GetRefactoringsAsync(filePath, request.Range, token).ConfigureAwait(false));
            return new CodeActionResponse(result);
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

            var result = await codeAction.ResolveCodeActionAsync(workspaceService, token).ConfigureAwait(false);
            if (result == null) {
                currentClassLogger.Error($"CodeAction '{request.Title}' with id '{codeActionId}' failed to resolve");
                return request;
            }

            return result;
        });
    }

    private async Task<IEnumerable<CommandOrCodeAction>> GetQuickFixesAsync(string filePath, DocumentRange range, CancellationToken cancellationToken) {
        var documentIds = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(filePath);
        if (documentIds == null)
            return Enumerable.Empty<CommandOrCodeAction>();

        foreach (var documentId in documentIds) {
            var result = new List<CommandOrCodeAction>();
            var document = workspaceService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var textSpan = range.ToTextSpan(sourceText);
            var diagnosticContexts = codeAnalysisService.GetDiagnosticsByDocumentSpan(document, textSpan);
            var diagnosticGroups = diagnosticContexts.GroupBy(it => it.Diagnostic.Id).ToList();
            if (diagnosticGroups.Count == 0)
                continue;

            foreach (var group in diagnosticGroups) {
                var project = group.FirstOrDefault()?.RelatedProject;
                if (project == null) {
                    currentClassLogger.Debug($"Project not found for diagnostic id '{group.Key}'");
                    continue;
                }

                var codeFixProviders = codeAnalysisService.GetCodeFixProvidersForDiagnosticId(group.Key, project);
                if (codeFixProviders == null) {
                    currentClassLogger.Debug($"CodeFixProviders not found for diagnostic id '{group.Key}'");
                    continue;
                }

                foreach (var codeFixProvider in codeFixProviders) {
                    var mergedTextSpan = group.Select(it => it!.Diagnostic.Location.SourceSpan).ToMergedTextSpan();
                    var diagnostics = group.Select(it => it!.Diagnostic).ToImmutableArray();
                    await codeFixProvider.RegisterCodeFixesAsync(new CodeFixContext(document, mergedTextSpan, diagnostics, (action, _) => {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        var singleCodeActions = action.ToFlattenCodeActions().Where(x => !x.IsBlacklisted());
                        foreach (var singleCodeAction in singleCodeActions) {
                            if (codeActionsCache.TryAdd(singleCodeAction.GetUniqueId(), singleCodeAction))
                                result.Add(new CommandOrCodeAction(singleCodeAction.ToCodeAction(CodeActionKind.QuickFix)));
                        }
                    }, cancellationToken)).ConfigureAwait(false);
                }
            }

            if (result.Count != 0)
                return result;
        }

        return Enumerable.Empty<CommandOrCodeAction>();
    }
    private async Task<IEnumerable<CommandOrCodeAction>> GetRefactoringsAsync(string filePath, DocumentRange range, CancellationToken cancellationToken) {
        var documentIds = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(filePath);
        if (documentIds == null)
            return Enumerable.Empty<CommandOrCodeAction>();

        foreach (var documentId in documentIds) {
            var document = workspaceService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var textSpan = range.ToTextSpan(sourceText);

            var result = new List<CommandOrCodeAction>();
            var codeRefactoringProviders = codeAnalysisService.GetCodeRefactoringProvidersForProject(document.Project);
            if (codeRefactoringProviders == null)
                continue;

            foreach (var refactoringProvider in codeRefactoringProviders) {
                await refactoringProvider.ComputeRefactoringsAsync(new CodeRefactoringContext(document, textSpan, action => {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    var codeActionPairs = action.ToFlattenCodeActions(CodeActionKind.Refactor);
                    foreach (var pair in codeActionPairs) {
                        if (pair.Item1.IsBlacklisted())
                            continue;
                        if (codeActionsCache.TryAdd(pair.Item1.GetUniqueId(), pair.Item1))
                            result.Add(new CommandOrCodeAction(pair.Item2));
                    }
                }, cancellationToken)).ConfigureAwait(false);
            }

            if (result.Count != 0)
                return result;
        }

        return Enumerable.Empty<CommandOrCodeAction>();
    }
}