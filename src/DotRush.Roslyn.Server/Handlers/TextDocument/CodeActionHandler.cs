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
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using CodeAnalysisCodeAction = Microsoft.CodeAnalysis.CodeActions.CodeAction;
using CodeAnalysisOperation = Microsoft.CodeAnalysis.CodeActions.ApplyChangesOperation;
using FileSystemExtensions = DotRush.Common.Extensions.FileSystemExtensions;

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

            var documentEdits = await ResolveCodeActionAsync(codeAction, workspaceService.Solution, token).ConfigureAwait(false);
            if (documentEdits.Count == 0) {
                currentClassLogger.Error($"CodeAction '{request.Title}' with id '{codeActionId}' has no text edits");
                return request;
            }

            request.Edit = new WorkspaceEdit() { Changes = documentEdits };
            return request;
        });
    }

    private async Task<IEnumerable<CommandOrCodeAction>> GetQuickFixesAsync(string filePath, DocumentRange range, CancellationToken cancellationToken) {
        var documentId = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(filePath).FirstOrDefault();
        if (documentId == null)
            return Enumerable.Empty<CommandOrCodeAction>();

        var result = new List<CommandOrCodeAction>();
        var document = workspaceService.Solution?.GetDocument(documentId);
        if (document == null)
            return result;

        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var textSpan = range.ToTextSpan(sourceText);
        var diagnosticContexts = codeAnalysisService.GetDiagnosticsByDocumentSpan(document, textSpan);
        var diagnosticByIdGroups = diagnosticContexts.GroupBy(it => it.Diagnostic.Id).ToList();
        if (diagnosticByIdGroups.Count == 0) {
            currentClassLogger.Debug($"No diagnostics found for document '{document.Name}' in range '{range}'");
            return result;
        }

        foreach (var byIdGroup in diagnosticByIdGroups) {
            // Original document with diagnostic was created. We need to use it to avoid:
            // System.ArgumentException: Syntax node is not within syntax tree
            document = byIdGroup.FirstOrDefault()?.Document;
            if (document == null) {
                currentClassLogger.Debug($"Document not found for diagnostic id '{byIdGroup.Key}'");
                continue;
            }

            var codeFixProviders = codeAnalysisService.GetCodeFixProvidersForDiagnosticId(byIdGroup.Key, document.Project);
            if (codeFixProviders == null) {
                currentClassLogger.Debug($"CodeFixProviders not found for diagnostic id '{byIdGroup.Key}'");
                continue;
            }

            foreach (var codeFixProvider in codeFixProviders) {
                var diagnosticByRangeGroups = byIdGroup.GroupBy(it => it.Diagnostic.Location.SourceSpan).ToList();
                foreach (var byRangeGroup in diagnosticByRangeGroups) {
                    var diagnostics = byRangeGroup.Select(it => it!.Diagnostic).ToImmutableArray();
                    await codeFixProvider.RegisterCodeFixesAsync(new CodeFixContext(document, byRangeGroup.Key, diagnostics, (action, _) => {
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
        }

        return result;
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
    private async Task<Dictionary<DocumentUri, List<TextEdit>>> ResolveCodeActionAsync(CodeAnalysisCodeAction codeAction, Solution solution, CancellationToken cancellationToken) {
        var documentEdits = new Dictionary<DocumentUri, List<TextEdit>>();
        var operations = await codeAction.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var operation in operations) {
            if (operation is not CodeAnalysisOperation applyChangesOperation)
                continue;

            var solutionChanges = applyChangesOperation.ChangedSolution.GetChanges(solution);
            foreach (var projectChanges in solutionChanges.GetProjectChanges()) {
                foreach (var documentId in projectChanges.GetChangedDocuments()) {
                    var newDocument = projectChanges.NewProject.GetDocument(documentId);
                    var oldDocument = solution.GetDocument(newDocument?.Id);
                    if (oldDocument?.FilePath == null || newDocument?.FilePath == null)
                        continue;

                    if (newDocument.Name != oldDocument.Name) {
                        ProcessDocumentRename(oldDocument, newDocument.Name);
                        continue;
                    }

                    var sourceText = await oldDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    var textEdits = new List<TextEdit>();
                    var textChanges = await newDocument.GetTextChangesAsync(oldDocument, cancellationToken).ConfigureAwait(false);
                    textEdits.AddRange(textChanges.Select(x => new TextEdit() {
                        NewText = x.NewText ?? string.Empty,
                        Range = x.Span.ToRange(sourceText),
                    }));

                    if (textEdits.Count != 0)
                        documentEdits.TryAdd(newDocument.FilePath, textEdits);
                }
                foreach (var documentId in projectChanges.GetAddedDocuments()) {
                    var newDocument = projectChanges.NewProject.GetDocument(documentId)!;
                    var sourceText = await newDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    ProcessDocumentCreate(newDocument, sourceText);
                }
                foreach (var documentId in projectChanges.GetRemovedDocuments()) {
                    var oldDocument = projectChanges.OldProject.GetDocument(documentId)!;
                    ProcessDocumentRemove(oldDocument);
                }
            }
        }

        return documentEdits;
    }

    private void ProcessDocumentCreate(Document document, SourceText sourceText) {
        if (document == null)
            return;
        var documentFilePath = document.FilePath;
        if (string.IsNullOrEmpty(documentFilePath)) {
            documentFilePath = document.Project.GetProjectDirectory();
            document.Folders.ForEach(folder => documentFilePath = Path.Combine(documentFilePath, folder));
            documentFilePath = Path.Combine(documentFilePath, document.Name);
        }

        FileSystemExtensions.WriteAllText(documentFilePath, sourceText.ToString());
        workspaceService.CreateDocument(documentFilePath);
        currentClassLogger.Debug($"File created via CodeAction: {documentFilePath}");
    }
    private void ProcessDocumentRemove(Document document) {
        if (document?.FilePath == null)
            return;

        workspaceService.DeleteDocument(document.FilePath);
        FileSystemExtensions.TryDeleteFile(document.FilePath);
        currentClassLogger.Debug($"File removed via CodeAction: {document.FilePath}");
    }
    private void ProcessDocumentRename(Document document, string newName) {
        if (document.FilePath == null || string.IsNullOrEmpty(newName))
            return;

        var newFilePath = FileSystemExtensions.RenameFile(document.FilePath, newName);
        workspaceService.DeleteDocument(document.FilePath);
        workspaceService.CreateDocument(newFilePath);
        currentClassLogger.Debug($"File renamed via CodeAction: {document.FilePath} -> {newFilePath}");
    }
}