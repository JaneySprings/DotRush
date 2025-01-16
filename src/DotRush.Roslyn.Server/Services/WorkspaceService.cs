using System.Collections.ObjectModel;
using DotRush.Roslyn.Common.External;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Workspaces;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace DotRush.Roslyn.Server.Services;

public class WorkspaceService : DotRushWorkspace {
    private readonly ConfigurationService configurationService;
    private readonly ILanguageServerFacade? serverFacade;
    private readonly IServerWorkDoneManager? workDoneManager;
    private IWorkDoneObserver? workDoneObserver;

    protected override ReadOnlyDictionary<string, string> WorkspaceProperties => configurationService.WorkspaceProperties;
    protected override bool LoadMetadataForReferencedProjects => configurationService.LoadMetadataForReferencedProjects;
    protected override bool SkipUnrecognizedProjects => configurationService.SkipUnrecognizedProjects;
    protected override bool RestoreProjectsBeforeLoading => configurationService.RestoreProjectsBeforeLoading;
    protected override bool CompileProjectsAfterLoading => configurationService.CompileProjectsAfterLoading;

    public WorkspaceService(ConfigurationService configurationService, ILanguageServerFacade? serverFacade, IServerWorkDoneManager? workDoneManager) {
        this.configurationService = configurationService;
        this.workDoneManager = workDoneManager;
        this.serverFacade = serverFacade;
    }

    public async Task LoadAsync(CancellationToken cancellationToken) {
        var targets = TryGetTargets();
        if (targets == null) {
            serverFacade?.ShowError("Found more than one project or solution file. Specify the `dotrush.roslyn.projectOrSolutionFiles` configuration property.");
            return;
        }

        var solutionFiles = targets.Where(it => 
            Path.GetExtension(it).Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(it).Equals(".slnf", StringComparison.OrdinalIgnoreCase)
        );
        if (solutionFiles.Any())
            await LoadSolutionAsync(solutionFiles, cancellationToken).ConfigureAwait(false);

        var projectFiles = targets.Where(it => Path.GetExtension(it).Equals(".csproj", StringComparison.OrdinalIgnoreCase));
        if (projectFiles.Any())
            await LoadProjectsAsync(projectFiles, cancellationToken).ConfigureAwait(false);
    }

    public override async Task OnLoadingStartedAsync(CancellationToken cancellationToken) {
        if (workDoneManager == null)
            return;
        workDoneObserver = await workDoneManager.Create(new WorkDoneProgressBegin(), cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    public override Task OnLoadingCompletedAsync(CancellationToken cancellationToken) {
        workDoneObserver?.OnCompleted();
        workDoneObserver?.Dispose();
        return Task.CompletedTask;
    }
    public override void OnProjectRestoreStarted(string documentPath) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        workDoneObserver?.OnNext(new WorkDoneProgressReport { Message = string.Format(null, Resources.ProjectRestoreCompositeFormat, projectName) });
    }
    public override void OnProjectLoadStarted(string documentPath) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        workDoneObserver?.OnNext(new WorkDoneProgressReport { Message = string.Format(null, Resources.ProjectIndexCompositeFormat, projectName) });
    }
    public override void OnProjectCompilationStarted(string documentPath) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        workDoneObserver?.OnNext(new WorkDoneProgressReport { Message = string.Format(null, Resources.ProjectCompileCompositeFormat, projectName) });
    }
    public override void OnProjectRestoreFailed(string documentPath, ProcessResult result) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        var message = string.Join(Environment.NewLine, result.ErrorLines.Count == 0 ? result.OutputLines : result.ErrorLines);
        serverFacade?.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams() {
            Uri = DocumentUri.FromFileSystemPath(documentPath),
            Diagnostics = new Container<Diagnostic>([new Diagnostic() {
                Message = string.Format(null, Resources.ProjectRestoreFailedCompositeFormat, projectName, message),
                Range = PositionExtensions.EmptyRange,
                Severity = DiagnosticSeverity.Error,
                Source = projectName,
                Code = "NU0000",
            }]),
        });
    }

    private IEnumerable<string>? TryGetTargets() {
        if (configurationService.ProjectOrSolutionFiles.Count != 0)
            return configurationService.ProjectOrSolutionFiles;

        var workspaceFolders = serverFacade?.Client.ClientSettings.WorkspaceFolders?.Select(it => it.Uri.GetFileSystemPath());
        if (workspaceFolders == null)
            return null;
        var solutionFiles = workspaceFolders.SelectMany(it => Directory.GetFiles(it, "*.sln", SearchOption.AllDirectories));
        if (solutionFiles.Count() == 1)
            return solutionFiles;
        var projectFiles = workspaceFolders.SelectMany(it => Directory.GetFiles(it, "*.csproj", SearchOption.AllDirectories));
        if (projectFiles.Count() == 1)
            return projectFiles;

        return null;
    }
}