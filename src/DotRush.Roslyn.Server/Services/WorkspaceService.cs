using System.Collections.ObjectModel;
using DotRush.Common.Extensions;
using DotRush.Common.InteropV2;
using DotRush.Common.MSBuild;
using DotRush.Protocol;
using DotRush.Protocol.Models;
using DotRush.Roslyn.Workspaces;
using DotRush.Roslyn.Workspaces.Extensions;
using DotRush.Roslyn.Workspaces.FileSystem;

namespace DotRush.Roslyn.Server.Services;

public class WorkspaceService : DotRushWorkspace, IWorkspaceChangeListener, IDisposable {
    private readonly ConfigurationService configurationService;
    private readonly LanguageServer? serverFacade;
    private WorkspaceFilesWatcher? fileWatcher;
    private ProgressReporter? progressReporter;

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

        await LoadAsync(targets, cancellationToken);
        StartObserving();
    }

    public override async Task OnLoadingStartedAsync(CancellationToken cancellationToken) {
        if (serverFacade != null)
            progressReporter = await SafeExtensions.InvokeAsync(() => serverFacade.Client.CreateProgressAsync(string.Empty, cancellationToken));
    }
    public override Task OnLoadingCompletedAsync(CancellationToken cancellationToken) {
        progressReporter?.End();
        progressReporter = null;
        return Task.CompletedTask;
    }
    public override void OnProjectRestoreStarted(string documentPath, int progress) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        progressReporter?.Report(string.Format(null, Resources.ProjectRestoreCompositeFormat, projectName), progress);
    }
    public override void OnProjectRestoreCompleted(string documentPath, ProcessResult result) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        var diagnostics = new List<Diagnostic>();
        if (result.ExitCode != 0) {
            var message = string.Join(Environment.NewLine, result.ErrorLines.Count == 0 ? result.OutputLines : result.ErrorLines);
            diagnostics.Add(new Diagnostic() {
                Message = string.Format(null, Resources.ProjectRestoreFailedCompositeFormat, projectName, message),
                Range = default(DocumentRange),
                Severity = DiagnosticSeverity.Error,
                Source = projectName,
                Code = "NU0000",
            });
        }
        serverFacade?.Client.PublishDiagnostics(new PublishDiagnosticsParams() {
            Uri = documentPath,
            Diagnostics = diagnostics,
        });
    }
    public override void OnProjectLoadStarted(string documentPath, int progress) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        progressReporter?.Report(string.Format(null, Resources.ProjectIndexCompositeFormat, projectName), progress);
    }
    public override void OnProjectLoadCompleted(Microsoft.CodeAnalysis.Project project) {
        var projectModel = MSBuildProjectsLoader.LoadProject(project.FilePath, true);
        if (projectModel != null)
            serverFacade?.Client.SendNotification(Resources.ProjectLoadedNotification, projectModel);
    }
    public override void OnProjectCompilationStarted(string documentPath, int progress) {
        var projectName = Path.GetFileNameWithoutExtension(documentPath);
        progressReporter?.Report(string.Format(null, Resources.ProjectCompileCompositeFormat, projectName), progress);
    }

    internal IEnumerable<string>? GetProjectOrSolutionFiles(IEnumerable<string>? workspaceFolders) {
        if (configurationService.ProjectOrSolutionFiles.Count != 0)
            return configurationService.ProjectOrSolutionFiles.Select(it => Path.GetFullPath(it.ToPlatformPath())).ToArray();

        if (workspaceFolders == null)
            workspaceFolders = new[] { Environment.CurrentDirectory };

        var solutionFiles = workspaceFolders.SelectMany(it => FileSystemExtensions.GetFirstFiles(it, [".sln", ".slnf", ".slnx"]));
        if (solutionFiles.Count() == 1)
            return solutionFiles;
        var projectFiles = workspaceFolders.SelectMany(it => FileSystemExtensions.GetFirstFiles(it, [".csproj"]));
        if (projectFiles.Count() == 1)
            return projectFiles;

        return null;
    }
    internal void StartObserving() {
        fileWatcher?.Dispose();
        if (Solution == null)
            return;

        fileWatcher = new WorkspaceFilesWatcher(this);
        foreach (var projectDirectory in Solution.Projects.Select(x => Path.GetDirectoryName(x.FilePath))) {
            if (!string.IsNullOrEmpty(projectDirectory))
                fileWatcher.AddDirectoryWatcher(projectDirectory);
        }
    }

    void IWorkspaceChangeListener.OnDocumentCreated(string documentPath) {
        CreateDocument(documentPath);
        if (ApplyWorkspaceChanges && WorkspaceExtensions.IsSourceCodeDocument(documentPath))
            DefaultItemsRewriter.AddCompilerItem(documentPath);
    }
    void IWorkspaceChangeListener.OnDocumentDeleted(string documentPath) {
        DeleteDocument(documentPath);
        // From FileSystemWatcher, we can get directory changes as well.
        Solution?.GetDocumentsWithDirectoryPath(documentPath)?.ForEach(x => DeleteDocument(x.FilePath ?? string.Empty));
        Solution?.GetAdditionalDocumentsWithDirectoryPath(documentPath)?.ForEach(x => DeleteDocument(x.FilePath ?? string.Empty));
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