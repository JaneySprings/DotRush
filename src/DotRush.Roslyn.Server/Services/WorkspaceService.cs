using System.Collections.ObjectModel;
using System.Text.Json;
using DotRush.Common.Extensions;
using DotRush.Common.InteropV2;
using DotRush.Common.MSBuild;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Workspaces;
using DotRush.Roslyn.Workspaces.Extensions;
using DotRush.Roslyn.Workspaces.FileSystem;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Client.PublishDiagnostics;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Diagnostic;
using EmmyLua.LanguageServer.Framework.Server;

namespace DotRush.Roslyn.Server.Services;

public sealed class WorkspaceService : DotRushWorkspace, IWorkspaceChangeListener, IDisposable {
    private readonly ConfigurationService configurationService;
    private readonly LanguageServer? serverFacade;
    private WorkspaceFilesWatcher? fileWatcher;

    protected override ReadOnlyDictionary<string, string> WorkspaceProperties => configurationService.WorkspaceProperties;
    protected override bool LoadMetadataForReferencedProjects => configurationService.LoadMetadataForReferencedProjects;
    protected override bool SkipUnrecognizedProjects => configurationService.SkipUnrecognizedProjects;
    protected override bool RestoreProjectsBeforeLoading => configurationService.RestoreProjectsBeforeLoading;
    protected override bool CompileProjectsAfterLoading => configurationService.CompileProjectsAfterLoading;
    protected override bool ApplyWorkspaceChanges => configurationService.ApplyWorkspaceChanges;
    protected override string DotNetSdkDirectory => configurationService.DotNetSdkDirectory;

    public WorkspaceService(ConfigurationService configurationService, LanguageServer? serverFacade) {
        this.configurationService = configurationService;
        this.serverFacade = serverFacade;
    }

    public async Task LoadAsync(IEnumerable<WorkspaceFolder>? workspaceFolderUris, CancellationToken cancellationToken) {
        var workspaceFolders = workspaceFolderUris?.Select(it => it.Uri.FileSystemPath).ToArray();
        var targets = GetProjectOrSolutionFiles(workspaceFolders);
        if (targets == null)
            return; //serverFacade?.ShowError(Resources.ProjectOrSolutionFileSpecificationRequired);

        await LoadAsync(targets, cancellationToken).ConfigureAwait(false);
        StartObserving(workspaceFolders);
    }

    public override async Task OnLoadingStartedAsync(CancellationToken cancellationToken) {
        await serverFacade.CreateWorkDoneProgress(Resources.WorkspaceServiceWorkDoneToken).ConfigureAwait(false);
    }
    public override async Task OnLoadingCompletedAsync(CancellationToken cancellationToken) {
        await serverFacade.EndWorkDoneProgress(Resources.WorkspaceServiceWorkDoneToken).ConfigureAwait(false);
    }
    public override void OnProjectRestoreStarted(string documentPath) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        _ = serverFacade?.UpdateWorkDoneProgress(Resources.WorkspaceServiceWorkDoneToken, string.Format(null, Resources.ProjectRestoreCompositeFormat, projectName));
    }
    public override void OnProjectLoadStarted(string documentPath) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        _ = serverFacade?.UpdateWorkDoneProgress(Resources.WorkspaceServiceWorkDoneToken, string.Format(null, Resources.ProjectIndexCompositeFormat, projectName));
    }
    public override void OnProjectLoadCompleted(Microsoft.CodeAnalysis.Project project) {
        var projectModel = MSBuildProjectsLoader.LoadProject(project.FilePath, true);
        _ = serverFacade?.SendNotification(Resources.ProjectLoadedNotification, JsonSerializer.SerializeToDocument(projectModel));
    }
    public override void OnProjectCompilationStarted(string documentPath) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        _ = serverFacade?.UpdateWorkDoneProgress(Resources.WorkspaceServiceWorkDoneToken, string.Format(null, Resources.ProjectCompileCompositeFormat, projectName));
    }
    public override void OnProjectRestoreFailed(string documentPath, ProcessResult result) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        var message = string.Join(Environment.NewLine, result.ErrorLines.Count == 0 ? result.OutputLines : result.ErrorLines);
        _ = serverFacade?.Client.PublishDiagnostics(new PublishDiagnosticsParams() {
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

    internal IEnumerable<string>? GetProjectOrSolutionFiles(IEnumerable<string>? workspaceFolders) {
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
    internal void StartObserving(string[]? workspaceFolders) {
        fileWatcher?.Dispose();
        if (workspaceFolders != null) {
            fileWatcher = new WorkspaceFilesWatcher(this);
            fileWatcher.StartObserving(workspaceFolders);
        }
    }

    void IWorkspaceChangeListener.OnDocumentCreated(string documentPath) {
        CreateDocument(documentPath);
        if (ApplyWorkspaceChanges && WorkspaceExtensions.IsSourceCodeDocument(documentPath))
            DefaultItemsRewriter.AddCompilerItem(documentPath);
    }
    void IWorkspaceChangeListener.OnDocumentDeleted(string documentPath) {
        DeleteDocument(documentPath);
        if (ApplyWorkspaceChanges)
            DefaultItemsRewriter.RemoveCompilerItem(documentPath);
    }
    void IWorkspaceChangeListener.OnDocumentChanged(string documentPath) {
        UpdateDocument(documentPath);
    }

    public void Dispose() {
        fileWatcher?.Dispose();
    }
}