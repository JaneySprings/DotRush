using DotRush.Server.Extensions;
using DotRush.Server.Services;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DotRush.Server.Handlers;

public class DiagnosticsHandler : DocumentDiagnosticHandlerBase {
    private readonly CompilationService compilationService;
    private readonly ConfigurationService configurationService;
    private readonly WorkspaceService workspaceService;
    private readonly ILanguageServerFacade serverFacade;

    public DiagnosticsHandler(CompilationService compilationService, ConfigurationService configurationService, WorkspaceService workspaceService, ILanguageServerFacade serverFacade) {
        this.compilationService = compilationService;
        this.configurationService = configurationService;
        this.workspaceService = workspaceService;
        this.serverFacade = serverFacade;
    }

    protected override DiagnosticsRegistrationOptions CreateRegistrationOptions(DiagnosticClientCapabilities capability, ClientCapabilities clientCapabilities) {
        return new DiagnosticsRegistrationOptions() {
            DocumentSelector = TextDocumentSelector.ForLanguage("csharp"),
            WorkspaceDiagnostics = false,
            InterFileDependencies = true,
        };
    }


    public override async Task<RelatedDocumentDiagnosticReport> Handle(DocumentDiagnosticParams request, CancellationToken cancellationToken) {
#pragma warning disable CS8603 
// Possible null reference argument.
        return await ServerExtensions.SafeHandlerAsync<RelatedDocumentDiagnosticReport?>(async() => {
            await workspaceService.WaitHandle;
            
            var documentPath = request.TextDocument.Uri.GetFileSystemPath();
            await compilationService.DiagnoseAsync(documentPath, cancellationToken);

            if (configurationService.IsRoslynAnalyzersEnabled())
                await compilationService.AnalyzerDiagnoseAsync(documentPath, cancellationToken);

            var diagnostics = compilationService.Diagnostics[documentPath].GetTotalServerDiagnostics();
            serverFacade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams() {
                Uri = DocumentUri.From(documentPath),
                Diagnostics = new Container<Diagnostic>(diagnostics),
            });
            
            return null;
        });
#pragma warning restore CS8603
    }
}