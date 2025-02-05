using System.Collections.ObjectModel;
using DotRush.Roslyn.Common.External;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Workspaces;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Client.PublishDiagnostics;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Diagnostic;

namespace DotRush.Roslyn.Server.Services;

public class WorkspaceService : DotRushWorkspace {
    private readonly ConfigurationService configurationService;

    protected override ReadOnlyDictionary<string, string> WorkspaceProperties => configurationService.WorkspaceProperties;
    protected override bool LoadMetadataForReferencedProjects => configurationService.LoadMetadataForReferencedProjects;
    protected override bool SkipUnrecognizedProjects => configurationService.SkipUnrecognizedProjects;
    protected override bool RestoreProjectsBeforeLoading => configurationService.RestoreProjectsBeforeLoading;
    protected override bool CompileProjectsAfterLoading => configurationService.CompileProjectsAfterLoading;
    protected override bool ApplyWorkspaceChanges => configurationService.ApplyWorkspaceChanges;

    public WorkspaceService(ConfigurationService configurationService) {
        this.configurationService = configurationService;
    }

    public async Task LoadAsync(IEnumerable<WorkspaceFolder>? workspaceFolders, CancellationToken cancellationToken) {
        var targets = TryGetTargets(workspaceFolders);
        if (targets == null) {
            LanguageServer.Proxy.ShowError("Found more than one project or solution file. Specify the `dotrush.roslyn.projectOrSolutionFiles` configuration property.");
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

    private IEnumerable<string>? TryGetTargets(IEnumerable<WorkspaceFolder>? workspaceFolderUris) {
        if (configurationService.ProjectOrSolutionFiles.Count != 0)
            return configurationService.ProjectOrSolutionFiles;

        var workspaceFolders = workspaceFolderUris?.Select(it => it.Uri.FileSystemPath);
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