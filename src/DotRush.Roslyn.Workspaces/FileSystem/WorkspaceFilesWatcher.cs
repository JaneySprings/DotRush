namespace DotRush.Roslyn.Workspaces.FileSystem;

public sealed class WorkspaceFilesWatcher : IDisposable {
    private readonly IWorkspaceChangeListener listener;
    private readonly List<FileSystemWatcher> fileWatchers;

    public WorkspaceFilesWatcher(IWorkspaceChangeListener listener) {
        this.listener = listener;
        this.fileWatchers = new List<FileSystemWatcher>();
    }

    public void StartObserving(string[] workspaceFolders) {
        if (fileWatchers.Count > 0)
            throw new InvalidOperationException("File watchers are already initialized.");

        foreach (var folderPath in workspaceFolders) {
            if (!Directory.Exists(folderPath))
                continue;

            var fileWatcher = new FileSystemWatcher {
                Path = folderPath,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                Filter = "*.*",
            };
            fileWatcher.Created += OnCreated;
            fileWatcher.Changed += OnChanged;
            fileWatcher.Deleted += OnDeleted;
            fileWatcher.Renamed += OnRenamed;
            fileWatchers.Add(fileWatcher);
        }
    }

    private void OnCreated(object source, FileSystemEventArgs e) {
        if (Directory.Exists(e.FullPath)) {
            foreach (var filePath in Directory.EnumerateFiles(e.FullPath, "*.*", SearchOption.AllDirectories))
                listener.OnDocumentCreated(filePath);
            return;
        }
        listener.OnDocumentCreated(e.FullPath);
    }
    private void OnChanged(object source, FileSystemEventArgs e) {
        if (Directory.Exists(e.FullPath)) {
            foreach (var filePath in Directory.EnumerateFiles(e.FullPath, "*.*", SearchOption.AllDirectories))
                listener.OnDocumentChanged(filePath);
            return;
        }
        listener.OnDocumentChanged(e.FullPath);
    }
    private void OnDeleted(object source, FileSystemEventArgs e) {
        listener.OnDocumentDeleted(e.FullPath);
    }
    private void OnRenamed(object sender, RenamedEventArgs e) {
        listener.OnDocumentDeleted(e.OldFullPath);
        listener.OnDocumentCreated(e.FullPath);
    }

    public void Dispose() {
        foreach (var watcher in fileWatchers) {
            watcher.Created -= OnCreated;
            watcher.Changed -= OnChanged;
            watcher.Deleted -= OnDeleted;
            watcher.Renamed -= OnRenamed;
            watcher.Dispose();
        }
        fileWatchers.Clear();
    }
}
