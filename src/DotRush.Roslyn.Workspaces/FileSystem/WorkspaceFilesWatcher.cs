using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.Workspaces.Extensions;

namespace DotRush.Roslyn.Workspaces.FileSystem;

public class WorkspaceFilesWatcher : IDisposable {
    private const int UpdateTreshold = 2500;

    private readonly CurrentClassLogger currentClassLogger;
    private readonly IWorkspaceChangeListener listener;
    private readonly Thread workerThread;
    private IEnumerable<string> workspaceFolders;
    private Dictionary<string, long> workspaceFiles;
    private string? repositoryPath;
    private bool isDisposed;

    public WorkspaceFilesWatcher(IWorkspaceChangeListener listener) {
        this.listener = listener;
        currentClassLogger = new CurrentClassLogger(nameof(WorkspaceFilesWatcher));
        workspaceFolders = Enumerable.Empty<string>();
        workspaceFiles = new Dictionary<string, long>();
        workerThread = new Thread(() => SafeExtensions.Invoke(() => {
            CheckWorkspaceChanges(); // Fill initial workspace files

            while (!isDisposed) {
                CheckWorkspaceChanges();
                Thread.Sleep(UpdateTreshold);
            }
        }));
        workerThread.IsBackground = true;
        workerThread.Name = nameof(WorkspaceFilesWatcher);
    }

    public void StartObserving(IEnumerable<string> workspaceFolders) {
        if (workerThread.ThreadState == ThreadState.Running)
            return;

        this.workspaceFolders = workspaceFolders;
        currentClassLogger.Debug($"Start observing workspace folders: {string.Join("; ", workspaceFolders)}");

        if (listener.IsGitEventsSupported)
            repositoryPath = GitExtensions.GetRepositoryFolder(workspaceFolders);

        workerThread.Start();
    }

    private void CheckWorkspaceChanges() {
        if (listener.IsGitEventsSupported && GitExtensions.IsRepositoryLocked(repositoryPath)) {
            currentClassLogger.Debug($"Repository '{repositoryPath}' is locked, skipping workspace changes check");
            return;
        }

        var newWorkspaceFiles = ProcessDirectoryFiles();
        if (workspaceFiles.Count == 0) {
            workspaceFiles = newWorkspaceFiles;
            return;
        }

        // Create files
        var hasChanges = false;
        var addedFiles = new HashSet<string>(newWorkspaceFiles.Keys.Except(workspaceFiles.Keys));
        if (addedFiles.Count > 0) {
            listener.OnDocumentsCreated(addedFiles);
            hasChanges = true;
        }

        // Delete files
        var removedFiles = new HashSet<string>(workspaceFiles.Keys.Except(newWorkspaceFiles.Keys));
        if (removedFiles.Count > 0) {
            listener.OnDocumentsDeleted(removedFiles);
            hasChanges = true;
        }

        // Update files
        var changedFiles = new HashSet<string>();
        foreach (var newFile in newWorkspaceFiles) {
            if (workspaceFiles.TryGetValue(newFile.Key, out var oldMetadata) && oldMetadata != newFile.Value)
                changedFiles.Add(newFile.Key);
        }
        if (changedFiles.Count > 0)
            listener.OnDocumentsChanged(changedFiles);

        // Commit changes
        workspaceFiles = newWorkspaceFiles;
        if (hasChanges)
            listener.OnCommitChanges();
    }
    private Dictionary<string, long> ProcessDirectoryFiles() {
        var newWorkspaceFiles = new Dictionary<string, long>(workspaceFiles.Count);
        var enumerationOptions = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
        foreach (var folder in workspaceFolders) {
            if (!Directory.Exists(folder))
                continue;

            foreach (var file in Directory.EnumerateFiles(folder, "*", enumerationOptions)) {
                if (!WorkspaceExtensions.IsRelevantDocument(file))
                    continue;

                newWorkspaceFiles[file] = File.GetLastWriteTime(file).Ticks;
            }
        }

        return newWorkspaceFiles;
    }

    public void Dispose() {
        if (workerThread.ThreadState == ThreadState.Unstarted || workerThread.ThreadState == ThreadState.Stopped)
            return;
        
        isDisposed = true;
        currentClassLogger.Debug("Stopped observing workspace folders");
    }
}
