namespace DotRush.Roslyn.Workspaces.FileSystem;

public class WorkspaceFilesWatcher {
    private const int UpdateTreshold = 1500;

    private readonly IWorkspaceChangeListener listener;
    private readonly Thread workerThread;
    private IEnumerable<string> workspaceFolders;
    private HashSet<string> workspaceFiles;

    public WorkspaceFilesWatcher(IWorkspaceChangeListener listener) {
        this.listener = listener;
        workspaceFolders = Enumerable.Empty<string>();
        workspaceFiles = new HashSet<string>();
        workerThread = new Thread(() => {
            while(true) {
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
        workerThread.Start();
    }

    private void CheckWorkspaceChanges() {
        var newWorkspaceFiles = new HashSet<string>();
        foreach (var folder in workspaceFolders) {
            if (!Directory.Exists(folder))
                continue;

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