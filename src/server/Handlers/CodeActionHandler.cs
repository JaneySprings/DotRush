using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using CodeAnalysisCodeAction = Microsoft.CodeAnalysis.CodeActions.CodeAction;
using DotRush.Server.Containers;

namespace DotRush.Server.Handlers;

public class CodeActionHandler : CodeActionHandlerBase {
    private readonly SolutionService solutionService;
    private readonly CompilationService compilationService;
    private readonly ConfigurationService configurationService;
    private readonly CodeActionService codeActionService;

    private List<CodeAnalysisCodeAction> codeActionsCollection;


    public CodeActionHandler(SolutionService solutionService, CompilationService compilationService, CodeActionService codeActionService, ConfigurationService configurationService) {
        this.codeActionsCollection = new List<CodeAnalysisCodeAction>();
        this.solutionService = solutionService;
        this.compilationService = compilationService;
        this.configurationService = configurationService;
        this.codeActionService = codeActionService;
    }

    protected override CodeActionRegistrationOptions CreateRegistrationOptions(CodeActionCapability capability, ClientCapabilities clientCapabilities) {
        return new CodeActionRegistrationOptions() {
            CodeActionKinds = new Container<CodeActionKind>(CodeActionKind.QuickFix),
            ResolveProvider = true,
        };
    }

    public override async Task<CommandOrCodeActionContainer> Handle(CodeActionParams request, CancellationToken cancellationToken) {
        return await ServerExtensions.SafeHandlerAsync<CommandOrCodeActionContainer>(new CommandOrCodeActionContainer(), async () => {
            var filePath = request.TextDocument.Uri.GetFileSystemPath();         
            this.codeActionsCollection.Clear();

            var result = new List<CodeAction>();
            var fileDiagnostic = GetDiagnosticByRange(request.Range, filePath);
            if (fileDiagnostic == null)
                return new CommandOrCodeActionContainer();

            var project = this.solutionService.Solution?.GetProject(fileDiagnostic.SourceId);
            if (project == null)
                return new CommandOrCodeActionContainer();

            var document = project.Documents.FirstOrDefault(x => x.FilePath == filePath);
            if (document == null)
                return new CommandOrCodeActionContainer();

            if (this.configurationService.IsRoslynAnalyzersEnabled())
                this.codeActionService.AddCodeFixProviders(project);

            var codeFixProviders = GetProvidersForDiagnosticId(fileDiagnostic.InnerDiagnostic.Id);
            if (codeFixProviders == null)
                return new CommandOrCodeActionContainer();

            foreach (var codeFixProvider in codeFixProviders) {
                await codeFixProvider.RegisterCodeFixesAsync(new CodeFixContext(document, fileDiagnostic.InnerDiagnostic, (a, _) => {
                    codeActionsCollection.Add(a);
                    result.Add(a.ToCodeAction());
                }, cancellationToken));
            }

            return new CommandOrCodeActionContainer(result.Select(x => new CommandOrCodeAction(x!)));
        });
    }
    public override async Task<CodeAction> Handle(CodeAction request, CancellationToken cancellationToken) {
        return await ServerExtensions.SafeHandlerAsync<CodeAction>(request, async () => {
            if (request.Data == null)
                return request;

            var codeAction = this.codeActionsCollection.FirstOrDefault(x => x.EquivalenceKey == request.Data.ToObject<string>());
            if (codeAction == null)
                return request;

            var result = await codeAction.ToCodeActionAsync(this.solutionService, cancellationToken);
            return result ?? request;
        });
    }

    private IEnumerable<CodeFixProvider>? GetProvidersForDiagnosticId(string? diagnosticId) {
        if (diagnosticId == null)
            return null;

        return this.codeActionService
            .GetCodeFixProviders()
            .Where(x => x.FixableDiagnosticIds.ContainsWithMapping(diagnosticId));
    }
    private SourceDiagnostic? GetDiagnosticByRange(ProtocolModels.Range range, string documentPath) {
        if (documentPath == null)
            return null;
        if (!this.compilationService.Diagnostics.ContainsKey(documentPath))
            return null;

        return this.compilationService.Diagnostics[documentPath]
            .GetTotalDiagnosticWrappers()
            .FirstOrDefault(x => x.InnerDiagnostic.Location.ToRange() == range);
    }
}