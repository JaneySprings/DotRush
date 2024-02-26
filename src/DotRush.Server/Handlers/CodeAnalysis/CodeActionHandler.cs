using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using CodeAnalysisCodeAction = Microsoft.CodeAnalysis.CodeActions.CodeAction;

namespace DotRush.Server.Handlers;

public class CodeActionHandler : CodeActionHandlerBase {
    private readonly WorkspaceService solutionService;
    private readonly CompilationService compilationService;
    private readonly CodeActionService codeActionService;
    private readonly List<CodeAnalysisCodeAction> codeActionsCollection;

    public CodeActionHandler(WorkspaceService solutionService, CompilationService compilationService, CodeActionService codeActionService) {
        codeActionsCollection = new List<CodeAnalysisCodeAction>();
        this.solutionService = solutionService;
        this.compilationService = compilationService;
        this.codeActionService = codeActionService;
    }

    protected override CodeActionRegistrationOptions CreateRegistrationOptions(CodeActionCapability capability, ClientCapabilities clientCapabilities) {
        return new CodeActionRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments,
            CodeActionKinds = new Container<CodeActionKind>(CodeActionKind.QuickFix),
            ResolveProvider = true,
        };
    }

    public override async Task<CommandOrCodeActionContainer?> Handle(CodeActionParams request, CancellationToken cancellationToken) {
        return await ServerExtensions.SafeHandlerAsync<CommandOrCodeActionContainer?>(async () => {
            var filePath = request.TextDocument.Uri.GetFileSystemPath();         
            codeActionsCollection.Clear();

            var diagnosticId = request.Context.Diagnostics.FirstOrDefault()?.Data?.ToObject<int>();
            if (diagnosticId == null)
                return null;
        
            var diagnostic = GetDiagnosticById(filePath, diagnosticId.Value);
            if (diagnostic == null)
                return null;

            var project = solutionService.Solution?.GetProject(diagnostic.SourceId);
            var documentId = solutionService.Solution?.GetDocumentIdsWithFilePath(filePath).FirstOrDefault(x => x.ProjectId == project?.Id);
            var document = solutionService.Solution?.GetDocument(documentId);
            if (project == null || document == null)
                return null;

            codeActionService.AddProjectProviders(project);

            var codeFixProviders = GetProvidersForDiagnosticId(diagnostic.InnerDiagnostic.Id, project.Id);
            if (codeFixProviders == null)
                return null;

            foreach (var codeFixProvider in codeFixProviders) {
                 await codeFixProvider.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic.InnerDiagnostic, (a, _) => {
                    var singleCodeActions = a.ToSingleCodeActions().Where(x => !x.IsBlacklisted());
                    codeActionsCollection.AddRange(singleCodeActions);
                }, cancellationToken));
            }

            return new CommandOrCodeActionContainer(codeActionsCollection.Select(x => new CommandOrCodeAction(x.ToCodeAction())));
        });
    }
    public override async Task<CodeAction> Handle(CodeAction request, CancellationToken cancellationToken) {
        return await ServerExtensions.SafeHandlerAsync<CodeAction>(request, async () => {
            if (request.Data == null)
                return request;

            var codeActionId = request.Data.ToObject<string>();
            var codeAction = codeActionsCollection.FirstOrDefault(x => string.IsNullOrEmpty(x.EquivalenceKey) ? codeActionId == x.Title : codeActionId == x.EquivalenceKey);
            if (codeAction == null)
                return request;

            var result = await codeAction.ToCodeActionAsync(solutionService, cancellationToken);
            return result ?? request;
        });
    }

    private IEnumerable<CodeFixProvider>? GetProvidersForDiagnosticId(string? diagnosticId, ProjectId projectId) {
        if (diagnosticId == null)
            return null;

        return codeActionService
            .GetCodeFixProviders(projectId)
            .Where(x => x.FixableDiagnosticIds.ContainsWithMapping(diagnosticId));
    }
    private ExtendedDiagnostic? GetDiagnosticById(string documentPath, int diagnosticId) {
        return compilationService.Diagnostics
            .GetDiagnostics(documentPath)?
            .FirstOrDefault(x => x.InnerDiagnostic.GetHashCode() == diagnosticId);
    }
}