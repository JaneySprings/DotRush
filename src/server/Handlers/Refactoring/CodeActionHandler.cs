using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;
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
            CodeActionKinds = new Container<CodeActionKind>(CodeActionKind.QuickFix),
            ResolveProvider = true,
        };
    }

    public override async Task<CommandOrCodeActionContainer> Handle(CodeActionParams request, CancellationToken cancellationToken) {
        return await ServerExtensions.SafeHandlerAsync<CommandOrCodeActionContainer>(new CommandOrCodeActionContainer(), async () => {
            var filePath = request.TextDocument.Uri.GetFileSystemPath();         
            codeActionsCollection.Clear();

            var fileDiagnostic = GetDiagnosticByRange(request.Range, filePath);
            if (fileDiagnostic == null)
                return new CommandOrCodeActionContainer();

            var project = solutionService.Solution?.GetProject(fileDiagnostic.SourceId);
            if (project == null)
                return new CommandOrCodeActionContainer();

            var documentId = solutionService.Solution?.GetDocumentIdsWithFilePath(filePath).FirstOrDefault(x => x.ProjectId == project.Id);
            var document = solutionService.Solution?.GetDocument(documentId);
            if (document == null)
                return new CommandOrCodeActionContainer();

            codeActionService.AddProjectProviders(project);

            var codeFixProviders = GetProvidersForDiagnosticId(fileDiagnostic.InnerDiagnostic.Id, project.Id);
            if (codeFixProviders == null)
                return new CommandOrCodeActionContainer();

            foreach (var codeFixProvider in codeFixProviders) {
                await codeFixProvider.RegisterCodeFixesAsync(new CodeFixContext(document, fileDiagnostic.InnerDiagnostic, (a, _) => {
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

            var codeAction = codeActionsCollection.FirstOrDefault(x => x.EquivalenceKey == request.Data.ToObject<string>());
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
    private SourceDiagnostic? GetDiagnosticByRange(ProtocolModels.Range range, string documentPath) {
        if (!this.compilationService.Diagnostics.ContainsKey(documentPath))
            return null;

        return compilationService.Diagnostics[documentPath]
            .GetTotalDiagnosticWrappers()
            .FirstOrDefault(x => x.InnerDiagnostic.Location.ToRange().Contains(range));
    }
}