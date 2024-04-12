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
    private readonly WorkspaceService solutionService;
    private readonly CodeAnalysisService codeAnalysisService;
    private readonly ConfigurationService configurationService;
    private readonly Dictionary<int, CodeAnalysisCodeAction> codeActionsCache;

    public CodeActionHandler(WorkspaceService solutionService, CodeAnalysisService codeAnalysisService, ConfigurationService configurationService) {
        codeActionsCache = new Dictionary<int, CodeAnalysisCodeAction>();
        this.solutionService = solutionService;
        this.codeAnalysisService = codeAnalysisService;
        this.configurationService = configurationService;
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
                CurrentSessionLogger.Error($"CodeAction '{request.Title}' data is null");
                return request;
            }

            var codeActionId = request.Data.ToObject<int>();
            if (!codeActionsCache.TryGetValue(codeActionId, out var codeAction)) {
                CurrentSessionLogger.Error($"CodeAction '{request.Title}' with id '{codeActionId}' not found");
                return request;
            }

            var result = await codeAction.ResolveCodeActionAsync(solutionService, cancellationToken).ConfigureAwait(false);
            if (result == null) {
                CurrentSessionLogger.Error($"CodeAction '{request.Title}' with id '{codeActionId}' failed to resolve");
                return request;
            }

            return result;
        });
    }

    private async Task<CommandOrCodeActionContainer> GetQuickFixesAsync(string filePath, IEnumerable<int> diagnosticIds, CancellationToken cancellationToken) {
        var result = new List<CommandOrCodeAction>();
        foreach (var diagnosticId in diagnosticIds) {
            var diagnostic = codeAnalysisService.CompilationHost.GetDiagnosticById(filePath, diagnosticId);
            if (diagnostic == null)
                return result;

            var source = configurationService.UseRoslynAnalyzers ? diagnostic.Source : null;
            var codeFixProviders = codeAnalysisService.CodeActionHost.GetCodeFixProvidersForDiagnosticId(diagnostic.InnerDiagnostic.Id, source);
            if (codeFixProviders == null)
                return result;

            var document = diagnostic.Source.Documents.FirstOrDefault(it => FileSystemExtensions.PathEquals(it.FilePath, filePath));
            if (document == null)
                return result;

            foreach (var codeFixProvider in codeFixProviders) {
                await codeFixProvider.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic.InnerDiagnostic, (action, _) => {
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