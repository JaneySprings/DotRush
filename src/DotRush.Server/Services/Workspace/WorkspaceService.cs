using DotRush.Server.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
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
            Uri = DocumentUri.FromFileSystemPath(projectFilePath),
            Diagnostics = new Container<Protocol.Diagnostic>(updatedDiagnostics),
        });
    }

    public bool TryInitializeWorkspace() {
        if (!LocatorExtensions.TryRegisterDefaults(() => serverFacade.Window.ShowError(Resources.MessageDotNetRegistrationFailed)))
            return false;

        workspace = MSBuildWorkspace.Create(configurationService.WorkspaceProperties);
        workspace.LoadMetadataForReferencedProjects = configurationService.LoadMetadataForReferencedProjects;
        workspace.SkipUnrecognizedProjects = configurationService.SkipUnrecognizedProjects;
        workspace.WorkspaceFailed += (_, d) => ProjectDiagnosticReceived(d.Diagnostic.ToServerDiagnostic());
        return true;
    }
    public async void StartSolutionLoading() {
        await LoadSolutionAsync(workspace!);
    }
}