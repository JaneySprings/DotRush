using DotRush.Server.Extensions;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Protocol = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Services;

public class WorkspaceService: SolutionService {
    private readonly ConfigurationService configurationService;
    private readonly ILanguageServerFacade serverFacade;
    private readonly List<Protocol.Diagnostic> worksapceDiagnostics;
    private MSBuildWorkspace? workspace;

    public WorkspaceService(ConfigurationService configurationService, ILanguageServerFacade serverFacade) {
        this.configurationService = configurationService;
        this.serverFacade = serverFacade;
        worksapceDiagnostics = new List<Protocol.Diagnostic>();
        MSBuildLocator.RegisterDefaults();
    }

    protected override void ClearDiagnostics() {
        worksapceDiagnostics.Clear();
    }
    protected override void ProjectDiagnosticReceived(Protocol.Diagnostic diagnostic) {
        worksapceDiagnostics.Add(diagnostic);
    }
    protected override void PushDiagnostics(string projectFilePath) {
        var updatedDiagnostics = worksapceDiagnostics.Select(it => it.UpdateSource(projectFilePath));
        serverFacade?.TextDocument?.PublishDiagnostics(new PublishDiagnosticsParams() {
            Uri = DocumentUri.From(projectFilePath),
            Diagnostics = new Container<Protocol.Diagnostic>(updatedDiagnostics),
        });
    }

    public void InitializeWorkspace() {
        workspace = MSBuildWorkspace.Create(configurationService.AdditionalWorkspaceArguments());
        workspace.LoadMetadataForReferencedProjects = configurationService.LoadMetadataForReferencedProjects();
        workspace.SkipUnrecognizedProjects = configurationService.SkipUnrecognizedProjects();
        workspace.WorkspaceFailed += (_, d) => ProjectDiagnosticReceived(d.Diagnostic.ToServerDiagnostic());
    }
    public async void StartSolutionLoading() {
        await LoadSolutionAsync(workspace!);
    }
}