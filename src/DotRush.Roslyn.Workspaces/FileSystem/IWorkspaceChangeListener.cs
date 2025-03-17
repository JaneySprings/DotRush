namespace DotRush.Roslyn.Workspaces.FileSystem;

public interface IWorkspaceChangeListener {
    public bool IsGitEventsSupported { get; }

    public void OnDocumentsCreated(IEnumerable<string> documentPaths);
    public void OnDocumentsDeleted(IEnumerable<string> documentPaths);
    public void OnDocumentsChanged(IEnumerable<string> documentPaths);
    public void OnCommitChanges();
}