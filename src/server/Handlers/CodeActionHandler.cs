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
    private SolutionService solutionService;
    private CompilationService compilationService;
    private CodeActionService codeActionService;

    public CodeActionHandler(SolutionService solutionService, CompilationService compilationService, CodeActionService codeActionService) {
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
        var result = new List<CodeAction?>();
        var documentId = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath()).FirstOrDefault();
        if (documentId == null)
            return new CommandOrCodeActionContainer();


        var document = this.solutionService.Solution?.GetDocument(documentId);
        if (document == null)
            return new CommandOrCodeActionContainer();

        var sourceText = await document.GetTextAsync(cancellationToken);
        var fileDiagnostic = GetDiagnosticByRange(request.Range, sourceText, document);
        var codeFixProviders = GetProvidersForDiagnosticId(fileDiagnostic?.Id);
        if (fileDiagnostic == null || codeFixProviders == null || !codeFixProviders.Any())
            return new CommandOrCodeActionContainer();

        foreach (var codeFixProvider in codeFixProviders) {
            await codeFixProvider.RegisterCodeFixesAsync(new CodeFixContext(document, fileDiagnostic, (a, _) => {
                this.codeActionService.CodeActions.SetCodeAction(a, a.GetHashCode(), document.FilePath!);
                result.Add(a.ToCodeAction());
            }, cancellationToken));
        }

        return new CommandOrCodeActionContainer(result.Where(x => x != null).Select(x => new CommandOrCodeAction(x!)));
    }
    public override async Task<CodeAction> Handle(CodeAction request, CancellationToken cancellationToken) {
        if (request.Data == null)
            return request;

        var codeAction = this.codeActionService.CodeActions.GetCodeAction(request.Data.ToObject<int>());
        if (codeAction == null)
            return request;

        var result = await codeAction.ToCodeAction(this.solutionService, cancellationToken);
        if (result == null)
            return request;

        return result;
    }

    private IEnumerable<CodeFixProvider>? GetProvidersForDiagnosticId(string? diagnosticId) {
        if (diagnosticId == null)
            return null;

        return this.codeActionService.CodeFixProviders?.Where(x => x.FixableDiagnosticIds.Contains(diagnosticId));
    }
    private CodeAnalysis.Diagnostic? GetDiagnosticByRange(ProtocolModels.Range range, SourceText sourceText, Document document) {
        if (!this.compilationService.Diagnostics.ContainsKey(document.FilePath!))
            return null;

        return this.compilationService.Diagnostics[document.FilePath!]
            .GetTotalDiagnostics()
            .FirstOrDefault(x => x.Location.SourceSpan.Contains(range.ToTextSpan(sourceText)));
    }
}