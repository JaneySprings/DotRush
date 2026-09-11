using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using DotRush.Common;
using DotRush.Common.Extensions;
using DotRush.Common.InteropV2;
using DotRush.Common.Logging;
using DotRush.Common.MSBuild;
using DotRush.Roslyn.Workspaces.Components;
using DotRush.Roslyn.Workspaces.Extensions;
using DotRush.Roslyn.Workspaces.MSBuild;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using FileSystemExtensions = DotRush.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.Workspaces;

/// <summary>
/// Loads projects through the MSBuild CLI into a Roslyn <see cref="Solution"/> and keeps it in sync with the file system.
/// Options and progress reporting are provided by the derived class.
/// </summary>
public abstract class WorkspaceHost {
    // Every design-time build is a separate 'dotnet msbuild' process, a few of them can run at once
    private static readonly int maxParallelBuilds = Math.Clamp(Environment.ProcessorCount / 2, 2, 6);

    private readonly ConcurrentDictionary<ProjectId, ProjectFileInfo> loadedProjects = new ConcurrentDictionary<ProjectId, ProjectFileInfo>();
    private readonly WorkspaceProgressHandler progressHandler = new WorkspaceProgressHandler();
    private AdhocWorkspace? hostWorkspace;

    public Solution? Solution { get; private set; }
    public event EventHandler? WorkspaceStateChanged;

    protected abstract ReadOnlyDictionary<string, string> WorkspaceProperties { get; }
    protected abstract bool LoadMetadataForReferencedProjects { get; }
    protected abstract bool SkipUnrecognizedProjects { get; }
    protected abstract bool RestoreProjectsBeforeLoading { get; }
    protected abstract bool CompileProjectsAfterLoading { get; }
    protected abstract string DotNetSdkDirectory { get; }

    public virtual Task OnLoadingStartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual Task OnLoadingCompletedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual void OnProjectRestoreStarted(string documentPath, int progress) { }
    public virtual void OnProjectRestoreCompleted(string documentPath, ProcessResult result) { }
    public virtual void OnProjectLoadStarted(string documentPath, int progress) { }
    public virtual void OnProjectLoadCompleted(Project project) { }
    public virtual void OnProjectCompilationStarted(string documentPath, int progress) { }
    public virtual void OnProjectCompilationCompleted(string documentPath) { }
    protected virtual void OnWorkspaceStateChanged(Solution newSolution) {
        Solution = newSolution;
        WorkspaceStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool InitializeWorkspace() {
        // Only hosts the Roslyn services, the solution itself is tracked by this class
        hostWorkspace ??= new AdhocWorkspace(MefHostServices.DefaultHost, WorkspaceKind.Host);
        return SafeExtensions.Invoke(false, () => {
            if (!string.IsNullOrEmpty(DotNetSdkDirectory))
                MSBuildLocator.RegisterMSBuildPath(DotNetSdkDirectory);
            return true;
        });
    }

    public async Task LoadAsync(IEnumerable<string> targets, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(hostWorkspace);
        await OnLoadingStartedAsync(cancellationToken).ConfigureAwait(false);
        try {
            var targetPaths = targets.Select(Path.GetFullPath).Where(it => WorkspaceExtensions.IsSolutionFile(it) || WorkspaceExtensions.IsProjectFile(it)).ToArray();
            var solutionPath = targetPaths.FirstOrDefault(WorkspaceExtensions.IsSolutionFile);
            var projectPaths = targetPaths.SelectMany(it => WorkspaceExtensions.IsSolutionFile(it) ? MSBuildSolutionLoader.GetProjectFiles(it) : new[] { it }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            CurrentSessionLogger.Debug($"Loading {projectPaths.Length} projects from: {string.Join(';', targetPaths)}");

            progressHandler.Reset();
            progressHandler.ScheduleOperations((RestoreProjectsBeforeLoading ? targetPaths.Length : 0) + projectPaths.Length);
            if (RestoreProjectsBeforeLoading)
                await RestoreAsync(targetPaths, cancellationToken).ConfigureAwait(false);

            // Analyzer assemblies are locked by the process on Windows and break subsequent builds (issues/33)
            var analyzerLoader = RuntimeInfo.IsWindows ? new ShadowCopyAnalyzerLoader() : (IAnalyzerAssemblyLoader)new DirectAnalyzerLoader();
            try {
                var fileInfos = await BuildProjectsAsync(projectPaths, cancellationToken).ConfigureAwait(false);
                var projectInfoFactory = new ProjectInfoFactory(new MetadataReferenceCache(), analyzerLoader, fileInfos);
                var solutionInfo = SolutionInfo.Create(SolutionId.CreateNewId(solutionPath), VersionStamp.Create(), solutionPath, fileInfos.Select(projectInfoFactory.Create).ToArray());
                hostWorkspace.ClearSolution();
                var solution = hostWorkspace.AddSolution(solutionInfo);

                loadedProjects.Clear();
                foreach (var fileInfo in fileInfos)
                    loadedProjects[projectInfoFactory.GetProjectId(fileInfo)] = fileInfo;
                OnWorkspaceStateChanged(solution);

                if (CompileProjectsAfterLoading)
                    await CompileAsync(solution, cancellationToken).ConfigureAwait(false);
                foreach (var project in solution.Projects.DistinctBy(it => it.FilePath))
                    OnProjectLoadCompleted(project);
                CurrentSessionLogger.Debug($"Workspace loading completed, loaded {solution.ProjectIds.Count} projects");
            } finally {
                (analyzerLoader as IDisposable)?.Dispose();
            }
        } finally {
            await OnLoadingCompletedAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void CreateDocument(string file) => CreateDocuments(new[] { file });

    /// <summary>
    /// Adds new files to the projects whose Compile or AdditionalFiles items include them. Candidates are the projects
    /// whose directory contains the file, the decision is MSBuild's: each candidate is design-time built once for the whole batch.
    /// </summary>
    public void CreateDocuments(IEnumerable<string> files) {
        if (Solution == null)
            return;

        var existingFiles = new List<string>();
        var candidateFiles = new Dictionary<ProjectId, List<string>>();
        foreach (var file in files.Where(File.Exists).Select(Path.GetFullPath)) {
            if (Solution.GetDocumentIdsWithFilePath(file).Length != 0) {
                existingFiles.Add(file);
                continue;
            }
            // Only file types that can be Compile or AdditionalFiles items are worth an MSBuild build
            if (!WorkspaceExtensions.IsAdditionalDocument(file) && !Path.GetExtension(file).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var (projectId, fileInfo) in loadedProjects.Where(it => it.Value.CanContainDocument(file))) {
                if (!candidateFiles.TryGetValue(projectId, out var projectFiles))
                    candidateFiles[projectId] = projectFiles = new List<string>();
                projectFiles.Add(file);
            }
        }

        foreach (var (projectId, projectFiles) in candidateFiles)
            SafeExtensions.Invoke(() => AddProjectItems(projectId, projectFiles));
        existingFiles.ForEach(file => UpdateDocument(file));
    }
    public void UpdateDocument(string file, string? text = null) {
        var filePath = Path.GetFullPath(file);
        var documentIds = Solution?.GetDocumentIdsWithFilePath(filePath) ?? ImmutableArray<DocumentId>.Empty;
        text ??= FileSystemExtensions.TryReadText(filePath);
        if (documentIds.Length == 0 || text == null)
            return;

        var sourceText = SourceText.From(text);
        foreach (var documentId in documentIds) {
            var solution = Solution!;
            if (solution.ContainsDocument(documentId))
                OnWorkspaceStateChanged(solution.WithDocumentText(documentId, sourceText));
            else if (solution.ContainsAdditionalDocument(documentId))
                OnWorkspaceStateChanged(solution.WithAdditionalDocumentText(documentId, sourceText));
            else if (solution.ContainsAnalyzerConfigDocument(documentId))
                OnWorkspaceStateChanged(solution.WithAnalyzerConfigDocumentText(documentId, sourceText));
        }
    }
    /// <summary>
    /// Removes the file from the projects that contain it. Documents generated by the build (in obj) are kept, the next build recreates them.
    /// </summary>
    public void DeleteDocument(string file) {
        var filePath = Path.GetFullPath(file);
        foreach (var documentId in Solution?.GetDocumentIdsWithFilePath(filePath) ?? ImmutableArray<DocumentId>.Empty) {
            var solution = Solution!;
            if (loadedProjects.TryGetValue(documentId.ProjectId, out var fileInfo) && fileInfo.IsBuildOutput(filePath))
                continue;
            if (solution.ContainsDocument(documentId))
                OnWorkspaceStateChanged(solution.RemoveDocument(documentId));
            else if (solution.ContainsAdditionalDocument(documentId))
                OnWorkspaceStateChanged(solution.RemoveAdditionalDocument(documentId));
        }
    }

    private async Task RestoreAsync(IEnumerable<string> targetPaths, CancellationToken cancellationToken) {
        foreach (var targetPath in targetPaths) {
            await SafeExtensions.InvokeAsync(async () => {
                OnProjectRestoreStarted(targetPath, progressHandler.GetProgress());
                var result = await MSBuildCli.RestoreAsync(targetPath, cancellationToken).ConfigureAwait(false);
                progressHandler.CompleteOperation();
                OnProjectRestoreCompleted(targetPath, result);
            }).ConfigureAwait(false);
        }
    }
    /// <summary>
    /// Design-time builds the projects and, unless <see cref="LoadMetadataForReferencedProjects"/> is set, every project they reference.
    /// </summary>
    private async Task<ImmutableArray<ProjectFileInfo>> BuildProjectsAsync(IEnumerable<string> projectPaths, CancellationToken cancellationToken) {
        var results = ImmutableArray.CreateBuilder<ProjectFileInfo>();
        var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var wave = projectPaths.Where(knownPaths.Add).ToList();
        using var limiter = new SemaphoreSlim(maxParallelBuilds);

        while (wave.Count != 0) {
            var waveResults = await Task.WhenAll(wave.Select(path => BuildProjectAsync(path, limiter, cancellationToken))).ConfigureAwait(false);
            wave = new List<string>();
            foreach (var fileInfos in waveResults) {
                results.AddRange(fileInfos);
                if (LoadMetadataForReferencedProjects)
                    continue;
                wave.AddRange(fileInfos.SelectMany(info => info.ProjectReferences).Where(path => File.Exists(path) && knownPaths.Add(path)));
            }
            progressHandler.ScheduleOperations(wave.Count);
        }
        return results.ToImmutable();
    }
    private async Task<ImmutableArray<ProjectFileInfo>> BuildProjectAsync(string projectPath, SemaphoreSlim limiter, CancellationToken cancellationToken) {
        await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            return await SafeExtensions.InvokeAsync(ImmutableArray<ProjectFileInfo>.Empty, async () => {
                if (!WorkspaceExtensions.IsProjectFile(projectPath)) {
                    if (!SkipUnrecognizedProjects)
                        throw new NotSupportedException($"Project '{projectPath}' is not supported");
                    CurrentSessionLogger.Debug($"Skipping unrecognized project '{projectPath}'");
                    progressHandler.CompleteOperation();
                    return ImmutableArray<ProjectFileInfo>.Empty;
                }

                OnProjectLoadStarted(projectPath, progressHandler.GetProgress());
                var fileInfos = await DesignTimeBuilder.BuildAsync(projectPath, WorkspaceProperties, cancellationToken).ConfigureAwait(false);
                progressHandler.CompleteOperation();
                return fileInfos;
            }).ConfigureAwait(false);
        } finally {
            limiter.Release();
        }
    }
    private async Task CompileAsync(Solution solution, CancellationToken cancellationToken) {
        progressHandler.ScheduleOperations(solution.ProjectIds.Count);
        foreach (var project in solution.Projects) {
            await SafeExtensions.InvokeAsync(async () => {
                OnProjectCompilationStarted(project.FilePath ?? project.Name, progressHandler.GetProgress());
                _ = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
                progressHandler.CompleteOperation();
                OnProjectCompilationCompleted(project.FilePath ?? project.Name);
            }).ConfigureAwait(false);
        }
    }

    private void AddProjectItems(ProjectId projectId, IReadOnlyList<string> files) {
        if (!loadedProjects.TryGetValue(projectId, out var fileInfo))
            return;

        var result = DesignTimeBuilder.GetCompilerItemsAsync(fileInfo, WorkspaceProperties, CancellationToken.None).GetAwaiter().GetResult();
        var compileItems = new HashSet<string>(result.GetItems("Compile").Select(item => item.FullPath), StringComparer.OrdinalIgnoreCase);
        var additionalItems = new HashSet<string>(result.GetItems("AdditionalFiles").Select(item => item.FullPath), StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in files) {
            if (compileItems.Contains(filePath))
                AddDocument(projectId, filePath, isAdditional: false);
            if (additionalItems.Contains(filePath))
                AddDocument(projectId, filePath, isAdditional: true);
        }
    }
    private void AddDocument(ProjectId projectId, string filePath, bool isAdditional) {
        var solution = Solution!;
        var project = solution.GetProject(projectId);
        if (project == null || solution.GetDocumentIdsWithFilePath(filePath).Any(id => id.ProjectId == projectId && (isAdditional ? solution.ContainsAdditionalDocument(id) : solution.ContainsDocument(id))))
            return;

        var sourceText = SourceText.From(FileSystemExtensions.TryReadText(filePath, string.Empty));
        var folders = ProjectExtensions.GetFolders(project.GetProjectDirectory(), filePath);
        var name = Path.GetFileName(filePath);
        var updatedSolution = isAdditional
            ? project.AddAdditionalDocument(name, sourceText, folders, filePath).Project.Solution
            : project.AddDocument(name, sourceText, folders, filePath).Project.Solution;
        OnWorkspaceStateChanged(updatedSolution);
    }
}
