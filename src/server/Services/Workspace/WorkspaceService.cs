using DotRush.Server.Extensions;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

namespace DotRush.Server.Services;

public class WorkspaceService: SolutionService {
    private const int MAX_WORKSPACE_ERRORS = 15;

    private TaskCompletionSource taskCompletionSource;
    private readonly ConfigurationService configurationService;
    private readonly ILanguageServerFacade serverFacade;
    private MSBuildWorkspace? workspace;
    private int workspaceErrorsCount;

    public Task WaitHandle => taskCompletionSource.Task;

    public WorkspaceService(ConfigurationService configurationService, ILanguageServerFacade serverFacade) {
        this.configurationService = configurationService;
        this.serverFacade = serverFacade;
        taskCompletionSource = new TaskCompletionSource();
        MSBuildLocator.RegisterDefaults();
    }

    protected override void ProjectFailed(object? sender, WorkspaceDiagnosticEventArgs e) {
        if ((workspaceErrorsCount < MAX_WORKSPACE_ERRORS) && !string.IsNullOrEmpty(e.Diagnostic.Message)) {
            serverFacade?.Window.ShowWarning(e.Diagnostic.Message);
            workspaceErrorsCount++;
        }
    }
    protected override void RestoreFailed(string message) {
        if (!string.IsNullOrEmpty(message))
            serverFacade.Window.ShowWarning(message);
    }

    public void InitializeWorkspace() {
        var options = configurationService.AdditionalWorkspaceArguments();
        workspace = MSBuildWorkspace.Create(options);
        workspace.LoadMetadataForReferencedProjects = true;
        workspace.SkipUnrecognizedProjects = true;
    }
    public async void StartSolutionLoading() {
        ArgumentNullException.ThrowIfNull(workspace);
        if (taskCompletionSource.Task.IsCompleted)
            taskCompletionSource = new TaskCompletionSource();

        await LoadSolutionAsync(workspace);
        taskCompletionSource.SetResult();
    }
    public void AddWorkspaceFolders(IEnumerable<string> workspaceFolders) {
        foreach (var folder in workspaceFolders) {
            if (!Directory.Exists(folder))
                continue;
            AddProjects(WorkspaceExtensions.GetVisibleFiles(folder, "*.csproj"));
        }
    }
}