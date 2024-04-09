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
    private readonly DiagnosticService diagnosticService;
    private readonly Dictionary<int, CodeAnalysisCodeAction> codeActionsCache;

    public CodeActionHandler(WorkspaceService solutionService, DiagnosticService diagnosticService) {
        codeActionsCache = new Dictionary<int, CodeAnalysisCodeAction>();
        this.solutionService = solutionService;
        this.diagnosticService = diagnosticService;
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
            var filePath = request.TextDocument.Uri.GetFileSystemPath();
            var serverDiagnostics = request.Context.Diagnostics.Where(it => it.Data?.ToObject<int>() != null);
            var diagnosticIds = serverDiagnostics.Select(it => it.Data!.ToObject<int>());
            codeActionsCache.Clear();

            var result = new List<CommandOrCodeAction>();
            foreach (var diagnosticId in diagnosticIds) {
                var diagnostic = diagnosticService.GetDiagnosticById(filePath, diagnosticId);
                if (diagnostic == null)
                    return null;

                var codeFixProviders = diagnosticService.GetCodeFixProvidersForDiagnosticId(diagnostic.InnerDiagnostic.Id, diagnostic.Source);
                if (codeFixProviders == null)
                    return null;

                var document = diagnostic.Source.Documents.FirstOrDefault(it => FileSystemExtensions.PathEquals(it.FilePath, filePath));
                if (document == null)
                    return null;

                foreach (var codeFixProvider in codeFixProviders) {
                    await codeFixProvider.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic.InnerDiagnostic, (action, _) => {
                        var singleCodeActions = action.ToSingleCodeActions().Where(x => !x.IsBlacklisted());
                        foreach (var singleCodeAction in singleCodeActions) {
                            if (codeActionsCache.TryAdd(singleCodeAction.GetUniqueId(), singleCodeAction))
                                result.Add(singleCodeAction.ToCodeAction());
                        }
                    }, cancellationToken)).ConfigureAwait(false);
                }
            }

            return new CommandOrCodeActionContainer(result);
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
}