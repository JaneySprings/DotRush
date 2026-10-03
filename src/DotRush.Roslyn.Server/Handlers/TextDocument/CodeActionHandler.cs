using System.Collections.Immutable;
using DotRush.Common.Extensions;
using DotRush.Protocol;
using DotRush.Protocol.Handlers;
using DotRush.Protocol.Models;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using ApplyChangesOperation = Microsoft.CodeAnalysis.CodeActions.ApplyChangesOperation;
using CodeAnalysisCodeAction = Microsoft.CodeAnalysis.CodeActions.CodeAction;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class CodeActionHandler : CodeActionHandlerBase {
    private readonly WorkspaceService workspaceService;
    private readonly CodeAnalysisService codeAnalysisService;
    private readonly Dictionary<int, CodeAnalysisCodeAction> codeActionsCache;

    public CodeActionHandler(WorkspaceService workspaceService, CodeAnalysisService codeAnalysisService) {
        codeActionsCache = new Dictionary<int, CodeAnalysisCodeAction>();
        this.workspaceService = workspaceService;
        this.codeAnalysisService = codeAnalysisService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities) {
        serverCapabilities.CodeActionProvider = new CodeActionOptions {
            CodeActionKinds = new List<CodeActionKind> { CodeActionKind.QuickFix, CodeActionKind.Refactor },
            ResolveProvider = true
        };
    }
    protected override async Task<List<CodeAction>> Handle(CodeActionParams request, CancellationToken token) {
        codeActionsCache.Clear();

        var result = new List<CodeAction>();
        var filePath = request.TextDocument.Uri.FileSystemPath;

        if (codeAnalysisService.CompilerDiagnosticsScope != AnalysisScope.None)
            result.AddRange(await GetQuickFixesAsync(filePath, request.Range, token));
        if (codeAnalysisService.AnalyzerDiagnosticsScope != AnalysisScope.None)
            result.AddRange(await GetRefactoringsAsync(filePath, request.Range, token));

        if (request.Context?.Only != null && request.Context.Only.Count > 0)
            return result.Where(it => it.Kind != null && request.Context.Only.Contains(it.Kind.Value)).ToList();

        return result;
    }
    protected override async Task<CodeAction?> Resolve(CodeAction request, CancellationToken token) {
        if (request.Data?.Value is not int codeActionId || !codeActionsCache.TryGetValue(codeActionId, out var codeAction) || workspaceService.Solution == null)
            throw new ProtocolException(Resources.CodeActionOutdated);

        var documentChanges = await ResolveCodeActionAsync(codeAction, workspaceService.Solution, token);
        request.Edit = new WorkspaceEdit() { DocumentChanges = documentChanges.ToList() };
        return request;
    }

    private async Task<IEnumerable<CodeAction>> GetQuickFixesAsync(string filePath, DocumentRange range, CancellationToken cancellationToken) {
        var documentId = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(filePath).FirstOrDefault();
        if (documentId == null)
            return Enumerable.Empty<CodeAction>();

        var result = new List<CodeAction>();
        var document = workspaceService.Solution?.GetDocument(documentId);
        if (document == null)
            return result;

        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var textSpan = range.ToTextSpan(sourceText);
        var diagnosticContexts = codeAnalysisService.GetDiagnosticsByDocumentSpan(document, textSpan);
        var contextByDocument = diagnosticContexts.GroupBy(it => it.Document).ToArray();
        foreach (var byDocumentGroup in contextByDocument) {
            // Original document with diagnostic was created. We need to use it to avoid:
            // System.ArgumentException: Syntax node is not within syntax tree
            document = byDocumentGroup.Key;
            if (document == null)
                continue;

            var contextByDiagnosticId = byDocumentGroup.GroupBy(it => it.Id).ToArray();
            foreach (var byIdGroup in contextByDiagnosticId) {
                var codeFixProviders = codeAnalysisService.GetCodeFixProvidersForDiagnosticId(byIdGroup.Key, document.Project);
                if (codeFixProviders == null)
                    continue;

                foreach (var codeFixProvider in codeFixProviders) {
                    var contextBySpan = byIdGroup.GroupBy(it => it.Diagnostic.Location.SourceSpan).ToArray();

                    foreach (var bySpanGroup in contextBySpan) {
                        var diagnostics = bySpanGroup.Select(it => it!.Diagnostic).ToImmutableArray();
                        // Regular QuickFix
                        await codeFixProvider.RegisterCodeFixesAsync(new CodeFixContext(document, bySpanGroup.Key, diagnostics, (action, _) => {
                            if (cancellationToken.IsCancellationRequested)
                                return;

                            action.ToFlattenCodeActions((codeAction, title) => {
                                if (codeActionsCache.TryAdd(codeAction.GetUniqueId(), codeAction))
                                    result.Add(codeAction.ToCodeAction(CodeActionKind.QuickFix, title));
                            });
                        }, cancellationToken)).ConfigureAwait(false);
                        // FixAll QuickFix
                        await codeFixProvider.RegisterFixAllCodeFixesAsync(document, byIdGroup.FirstOrDefault(), codeAnalysisService.DiagnosticProvider, action => {
                            if (cancellationToken.IsCancellationRequested)
                                return;

                            action.ToFlattenCodeActions((codeAction, title) => {
                                if (codeActionsCache.TryAdd(codeAction.GetUniqueId(), codeAction))
                                    result.Add(codeAction.ToCodeAction(CodeActionKind.QuickFix, title));
                            });
                        }, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        return result;
    }
    private async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(string filePath, DocumentRange range, CancellationToken cancellationToken) {
        var documentIds = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(filePath);
        if (documentIds == null)
            return Enumerable.Empty<CodeAction>();

        foreach (var documentId in documentIds) {
            var document = workspaceService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken);
            var textSpan = range.ToTextSpan(sourceText);

            var result = new List<CodeAction>();
            var codeRefactoringProviders = codeAnalysisService.GetCodeRefactoringProvidersForProject(document.Project);
            if (codeRefactoringProviders == null)
                continue;

            foreach (var refactoringProvider in codeRefactoringProviders) {
                await refactoringProvider.ComputeRefactoringsAsync(new CodeRefactoringContext(document, textSpan, action => {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    action.ToFlattenCodeActions((codeAction, title) => {
                        if (codeActionsCache.TryAdd(codeAction.GetUniqueId(), codeAction))
                            result.Add(codeAction.ToCodeAction(CodeActionKind.Refactor, title));
                    });
                }, cancellationToken));
            }

            if (result.Count != 0)
                return result;
        }

        return Enumerable.Empty<CodeAction>();
    }
    private async Task<IEnumerable<IDocumentChange>> ResolveCodeActionAsync(CodeAnalysisCodeAction codeAction, Solution solution, CancellationToken cancellationToken) {
        var documentChanges = new HashSet<IDocumentChange>(DocumentChangeEqualityComparer.Default);
        var operations = await codeAction.GetOperationsAsync(cancellationToken);
        foreach (var operation in operations) {
            if (operation is not ApplyChangesOperation applyChangesOperation)
                continue;

            var solutionChanges = applyChangesOperation.ChangedSolution.GetChanges(solution);
            documentChanges.AddRange(await solutionChanges.ToDocumentChangesAsync(cancellationToken));
        }

        return documentChanges;
    }
}
