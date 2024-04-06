using DotRush.Roslyn.Workspaces;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace DotRush.Roslyn.Server.Services;

public class WorkspaceService : DotRushWorkspace {
    private readonly ConfigurationService configurationService;
    private readonly ILanguageServerFacade serverFacade;
    private IWorkDoneObserver? workDoneObserver;

    protected override Dictionary<string, string> WorkspaceProperties => configurationService.WorkspaceProperties;
    protected override bool LoadMetadataForReferencedProjects => configurationService.LoadMetadataForReferencedProjects;
    protected override bool SkipUnrecognizedProjects => configurationService.SkipUnrecognizedProjects;

    public WorkspaceService(ConfigurationService configurationService, ILanguageServerFacade serverFacade) {
        this.configurationService = configurationService;
        this.serverFacade = serverFacade;
    }
}