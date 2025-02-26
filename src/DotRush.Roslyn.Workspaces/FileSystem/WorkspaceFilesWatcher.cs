using DotRush.Common.Extensions;
using DotRush.Common.Logging;

namespace DotRush.Roslyn.Workspaces.FileSystem;

public class WorkspaceFilesWatcher {
    private const int UpdateTreshold = 1500;

    private readonly CurrentClassLogger currentClassLogger;
    private readonly IWorkspaceChangeListener listener;
    private readonly Thread workerThread;
    private IEnumerable<string> workspaceFolders;
    private HashSet<string> workspaceFiles;
    private string? repositoryPath;

    public WorkspaceFilesWatcher(IWorkspaceChangeListener listener) {
        this.listener = listener;
        currentClassLogger = new CurrentClassLogger(nameof(WorkspaceFilesWatcher));
        workspaceFolders = Enumerable.Empty<string>();
        workspaceFiles = new HashSet<string>();
        workerThread = new Thread(() => {
            while (true) {
                Thread.Sleep(UpdateTreshold);
                CheckWorkspaceChanges();
            }
        });
        workerThread.IsBackground = true;
        workerThread.Name = nameof(WorkspaceFilesWatcher);
    }

    public void StartObserving(IEnumerable<string> workspaceFolders) {
        if (workerThread.ThreadState == ThreadState.Running)
            return;

        this.workspaceFolders = workspaceFolders;
        currentClassLogger.Debug($"Start observing workspace folders: {string.Join("; ", workspaceFolders)}");
        workerThread.Start();
    }

    private void CheckWorkspaceChanges() {
        if (listener.IsGitEventsSupported && GitExtensions.IsRepositoryLocked(repositoryPath)) {
            currentClassLogger.Debug($"Repository '{repositoryPath}' is locked, skipping workspace changes check");
            return;
        }

        var newWorkspaceFiles = new HashSet<string>();
        foreach (var folder in workspaceFolders) {
            if (!Directory.Exists(folder))
                continue;

            if (listener.IsGitEventsSupported && string.IsNullOrEmpty(repositoryPath))
                repositoryPath = GitExtensions.GetRepositoryFolder(folder);

            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                newWorkspaceFiles.Add(file);
        }

        if (workspaceFiles.Count == 0) {
            workspaceFiles = newWorkspaceFiles;
            return;
        }

        var hasChanges = false;
        var addedFiles = newWorkspaceFiles.Except(workspaceFiles);
        if (addedFiles.Any()) {
            listener.OnDocumentsCreated(addedFiles);
            hasChanges = true;
        }

        var removedFiles = workspaceFiles.Except(newWorkspaceFiles);
        if (removedFiles.Any()) {
            listener.OnDocumentsDeleted(removedFiles);
            hasChanges = true;
        }

        workspaceFiles = newWorkspaceFiles;
        if (hasChanges)
            listener.OnCommitChanges();
    }
}