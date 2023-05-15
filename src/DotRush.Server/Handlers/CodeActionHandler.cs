using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using CodeAnalysis = Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Server.Handlers;

public class CodeActionHandler : CodeActionHandlerBase {
    private Dictionary<int, CodeAnalysis.CodeActions.CodeAction> cachedCodeActions;
    private Dictionary<int, CodeAnalysis.Document> cachedDocuments;
    private SolutionService solutionService;
    private CompilationService compilationService;
    private CodeActionService codeActionService;

    public CodeActionHandler(SolutionService solutionService, CompilationService compilationService, CodeActionService codeActionService) {
        this.cachedCodeActions = new Dictionary<int, CodeAnalysis.CodeActions.CodeAction>();
        this.cachedDocuments = new Dictionary<int, CodeAnalysis.Document>();
        this.solutionService = solutionService;
        this.compilationService = compilationService;
        this.codeActionService = codeActionService;
    }

    protected override CodeActionRegistrationOptions CreateRegistrationOptions(CodeActionCapability capability, ClientCapabilities clientCapabilities) {
        return new CodeActionRegistrationOptions() {
            CodeActionKinds = new Container<CodeActionKind>(CodeActionKind.QuickFix),
            ResolveProvider = true,
        };
    }

    public override async Task<CommandOrCodeActionContainer> Handle(CodeActionParams request, CancellationToken cancellationToken) {
        this.cachedCodeActions.Clear();
        this.cachedDocuments.Clear();

        var codeActions = new List<CodeAction?>();
        var diagnostics = request.Context.Diagnostics;
        var documentIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
        if (!diagnostics.Any() || documentIds == null)
            return new CommandOrCodeActionContainer();

        foreach (var documentId in documentIds) {
            var document = this.solutionService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken);
            foreach (var diagnostic in diagnostics) {
                var fileDiagnostic = await GetDiagnosticByRange(diagnostic.Range, sourceText, document, cancellationToken);
                var codeFixProviders = GetProvidersForDiagnosticId(fileDiagnostic?.Id);
                if (fileDiagnostic == null || codeFixProviders == null || !codeFixProviders.Any())
                    continue;

                foreach (var codeFixProvider in codeFixProviders) {
                    var context = new CodeFixContext(document, fileDiagnostic, (a, _) => {
                        this.cachedCodeActions.Add(a.GetHashCode(), a);
                        this.cachedDocuments.Add(a.GetHashCode(), document);
                        codeActions.Add(a.ToCodeAction());
                    }, cancellationToken);
                    await codeFixProvider.RegisterCodeFixesAsync(context);
                }
            }
        }

        return new CommandOrCodeActionContainer(codeActions.Where(x => x != null).Select(x => new CommandOrCodeAction(x!)));
    }

    public override async Task<CodeAction> Handle(CodeAction request, CancellationToken cancellationToken) {
        if (request.Data == null)
            return request;

        var codeAction = this.cachedCodeActions[request.Data.ToObject<int>()];
        var document = this.cachedDocuments[request.Data.ToObject<int>()];
        var result = await codeAction.ToCodeAction(document, this.solutionService, cancellationToken);
        if (result == null)
            return request;

        return result;
    }


    private IEnumerable<CodeFixProvider>? GetProvidersForDiagnosticId(string? diagnosticId) {
        if (diagnosticId == null)
            return null;

        return this.codeActionService.CodeFixProviders?.Where(x => x.FixableDiagnosticIds.Contains(diagnosticId));
    }
    private async Task<CodeAnalysis.Diagnostic?> GetDiagnosticByRange(ProtocolModels.Range range, SourceText sourceText, Document document, CancellationToken cancellationToken) {
        var diagnostics = await this.compilationService.Diagnose(document, cancellationToken);
        if (diagnostics == null)
            return null;

        return diagnostics.FirstOrDefault(x => x.Location.SourceSpan.ToRange(sourceText) == range);
    }
}