using System.Collections.ObjectModel;
using System.Text.Json;
using DotRush.Common.Extensions;
using DotRush.Common.InteropV2;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Workspaces;
using DotRush.Roslyn.Workspaces.FileSystem;
using EmmyLua.LanguageServer.Framework.Protocol.JsonRpc;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Client.PublishDiagnostics;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Diagnostic;
using Project = Microsoft.CodeAnalysis.Project;

namespace DotRush.Roslyn.Server.Services;

public class WorkspaceService : DotRushWorkspace, IWorkspaceChangeListener {
    private readonly ConfigurationService configurationService;
    private readonly WorkspaceFilesWatcher fileWatcher;

    protected override ReadOnlyDictionary<string, string> WorkspaceProperties => configurationService.WorkspaceProperties;
    protected override bool LoadMetadataForReferencedProjects => configurationService.LoadMetadataForReferencedProjects;
    protected override bool SkipUnrecognizedProjects => configurationService.SkipUnrecognizedProjects;
    protected override bool RestoreProjectsBeforeLoading => configurationService.RestoreProjectsBeforeLoading;
    protected override bool CompileProjectsAfterLoading => configurationService.CompileProjectsAfterLoading;
    protected override bool ApplyWorkspaceChanges => configurationService.ApplyWorkspaceChanges;
    protected override string DotNetSdkDirectory => configurationService.DotNetSdkDirectory;

    public WorkspaceService(ConfigurationService configurationService) {
        this.configurationService = configurationService;
        this.fileWatcher = new WorkspaceFilesWatcher(this);
    }

    public async Task LoadAsync(IEnumerable<WorkspaceFolder>? workspaceFolderUris, CancellationToken cancellationToken) {
        var workspaceFolders = workspaceFolderUris?.Select(it => it.Uri.FileSystemPath).ToArray();
        var targets = GetProjectOrSolutionFiles(workspaceFolders);
        if (targets == null) {
            LanguageServer.Proxy.ShowError(Resources.ProjectOrSolutionFileSpecificationRequired);
            return;
        }

        await LoadAsync(targets, cancellationToken).ConfigureAwait(false);

        if (workspaceFolders != null)
            fileWatcher.StartObserving(workspaceFolders);
    }

    public override async Task OnLoadingStartedAsync(CancellationToken cancellationToken) {
        await LanguageServer.Server.CreateWorkDoneProgress(Resources.WorkspaceServiceWorkDoneToken).ConfigureAwait(false);
    }
    public override async Task OnLoadingCompletedAsync(CancellationToken cancellationToken) {
        await LanguageServer.Server.EndWorkDoneProgress(Resources.WorkspaceServiceWorkDoneToken).ConfigureAwait(false);
    }
    public override void OnProjectRestoreStarted(string documentPath) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        _ = LanguageServer.Server.UpdateWorkDoneProgress(Resources.WorkspaceServiceWorkDoneToken, string.Format(null, Resources.ProjectRestoreCompositeFormat, projectName));
    }
    public override void OnProjectLoadStarted(string documentPath) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        _ = LanguageServer.Server.UpdateWorkDoneProgress(Resources.WorkspaceServiceWorkDoneToken, string.Format(null, Resources.ProjectIndexCompositeFormat, projectName));
    }
    public override void OnProjectLoadCompleted(Project project) {
        _ = LanguageServer.Server.SendNotification(Resources.ProjectLoadedNotification, JsonSerializer.SerializeToDocument(project.FilePath));
    }
    public override void OnProjectCompilationStarted(string documentPath) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        _ = LanguageServer.Server.UpdateWorkDoneProgress(Resources.WorkspaceServiceWorkDoneToken, string.Format(null, Resources.ProjectCompileCompositeFormat, projectName));
    }
    public override void OnProjectRestoreFailed(string documentPath, ProcessResult result) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        var message = string.Join(Environment.NewLine, result.ErrorLines.Count == 0 ? result.OutputLines : result.ErrorLines);
        _ = LanguageServer.Proxy.PublishDiagnostics(new PublishDiagnosticsParams() {
            Uri = documentPath,
            Diagnostics = [new Diagnostic() {
                Message = string.Format(null, Resources.ProjectRestoreFailedCompositeFormat, projectName, message),
                Range = PositionExtensions.EmptyRange,
                Severity = DiagnosticSeverity.Error,
                Source = projectName,
                Code = "NU0000",
            }],
        });
    }

    private IEnumerable<string>? GetProjectOrSolutionFiles(IEnumerable<string>? workspaceFolders) {
        if (configurationService.ProjectOrSolutionFiles.Count != 0)
            return configurationService.ProjectOrSolutionFiles.Select(it => Path.GetFullPath(it.ToPlatformPath())).ToArray();

        if (workspaceFolders == null)
            workspaceFolders = new[] { Environment.CurrentDirectory };

        var solutionFiles = workspaceFolders.SelectMany(it => FileSystemExtensions.GetFiles(it, ["sln", "slnf", "slnx"], SearchOption.AllDirectories));
        if (solutionFiles.Count() == 1)
            return solutionFiles;
        var projectFiles = workspaceFolders.SelectMany(it => FileSystemExtensions.GetFiles(it, ["csproj"], SearchOption.AllDirectories));
        if (projectFiles.Count() == 1)
            return projectFiles;

        return null;
    }

    bool IWorkspaceChangeListener.IsGitEventsSupported {
        get => configurationService.ApplyWorkspaceChanges;
    }
    void IWorkspaceChangeListener.OnDocumentsCreated(IEnumerable<string> documentPaths) {
        CreateDocuments(documentPaths.ToArray());
    }
    void IWorkspaceChangeListener.OnDocumentsDeleted(IEnumerable<string> documentPaths) {
        DeleteDocuments(documentPaths.ToArray());
    }
    void IWorkspaceChangeListener.OnDocumentsChanged(IEnumerable<string> documentPaths) {
        UpdateDocuments(documentPaths.ToArray());
    }
    void IWorkspaceChangeListener.OnCommitChanges() {
        ApplyChanges();
    }
}