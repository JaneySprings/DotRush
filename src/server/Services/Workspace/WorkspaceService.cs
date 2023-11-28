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

    private TaskCompletionSource taskCompletionSource;
    private MSBuildWorkspace? workspace;

    public Task WaitHandle => taskCompletionSource.Task;

    public WorkspaceService(ConfigurationService configurationService, ILanguageServerFacade serverFacade) {
        this.configurationService = configurationService;
        this.serverFacade = serverFacade;
        taskCompletionSource = new TaskCompletionSource();
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
        var options = configurationService.AdditionalWorkspaceArguments();
        workspace = MSBuildWorkspace.Create(options);
        workspace.LoadMetadataForReferencedProjects = true;
        workspace.SkipUnrecognizedProjects = true;
        workspace.WorkspaceFailed += (_, d) => ProjectDiagnosticReceived(d.Diagnostic.ToServerDiagnostic());
    }
    public async void StartSolutionLoading() {
        ArgumentNullException.ThrowIfNull(workspace);
        if (taskCompletionSource.Task.IsCompleted)
            taskCompletionSource = new TaskCompletionSource();

        await LoadSolutionAsync(workspace);
        taskCompletionSource.SetResult();
    }
    public void AddWorkspaceFolders(IEnumerable<string> workspaceFolders) {
        foreach (var workspaceFolder in workspaceFolders) {
            if (!WorkspaceExtensions.GetVisibleFiles(workspaceFolder, "*.csproj").Any())
                continue;
            
            var projectFilePaths = Directory.GetFiles(workspaceFolder, "*.csproj");
            if (projectFilePaths != null && projectFilePaths.Length > 0) {
                AddProjects(projectFilePaths);
                continue;
            }

            var subDirectories = Directory.GetDirectories(workspaceFolder);
            AddWorkspaceFolders(subDirectories);
        }
    }
}