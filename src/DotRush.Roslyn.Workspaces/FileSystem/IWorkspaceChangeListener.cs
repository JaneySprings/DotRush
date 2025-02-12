namespace DotRush.Roslyn.Workspaces.FileSystem;

public interface IWorkspaceChangeListener {
    public void OnDocumentsCreated(IEnumerable<string> documentPaths);
    public void OnDocumentsDeleted(IEnumerable<string> documentPaths);
    public void OnCommitChanges();
}