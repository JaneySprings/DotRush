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
            CodeActionKinds = new Container<CodeActionKind>(CodeActionKind.QuickFix)
        };
    }

    public override async Task<CommandOrCodeActionContainer> Handle(CodeActionParams request, CancellationToken cancellationToken) {
        var codeActions = new List<CodeAction?>();
        var diagnostics = request.Context.Diagnostics;
        var document = this.solutionService.GetDocumentByPath(request.TextDocument.Uri.GetFileSystemPath());
        if (!diagnostics.Any() || document == null)
            return new CommandOrCodeActionContainer();

        var sourceText = await document.GetTextAsync(cancellationToken);
        foreach (var diagnostic in diagnostics) {
            var fileDiagnostic = GetDiagnosticByRange(diagnostic.Range, sourceText, document.FilePath!);
            var codeFixProviders = GetProvidersForDiagnosticId(fileDiagnostic?.Id);
            if (fileDiagnostic == null || codeFixProviders == null || !codeFixProviders.Any())
                continue;

            foreach (var codeFixProvider in codeFixProviders) {
                var context = new CodeFixContext(document, fileDiagnostic, async (a, _) => {
                    var action = await a.ToCodeAction(document, diagnostics, this.solutionService, cancellationToken);
                    codeActions.Add(action);
                }, cancellationToken);
                await codeFixProvider.RegisterCodeFixesAsync(context).WaitAsync(cancellationToken);
            }
        }

        return new CommandOrCodeActionContainer(codeActions.Where(x => x != null).Select(x => new CommandOrCodeAction(x!)));
    }

    public override Task<CodeAction> Handle(CodeAction request, CancellationToken cancellationToken) {
        return Task.FromResult(request);
    }


    private IEnumerable<CodeFixProvider>? GetProvidersForDiagnosticId(string? diagnosticId) {
        if (diagnosticId == null)
            return null;

        return this.codeActionService.CodeFixProviders?.Where(x => x.FixableDiagnosticIds.Contains(diagnosticId));
    }
    private CodeAnalysis.Diagnostic? GetDiagnosticByRange(ProtocolModels.Range range, SourceText sourceText, string path) {
        var diagnostics = this.compilationService.Diagnostics.TryGetValue(path, out var diags) ? diags : null;
        if (diagnostics == null)
            return null;

        return diagnostics.FirstOrDefault(x => x.Location.SourceSpan.ToRange(sourceText) == range);
    }
}