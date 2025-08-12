namespace DotRush.Roslyn.Workspaces.FileSystem;

public interface IWorkspaceChangeListener {
    public void OnDocumentCreated(string documentPath);
    public void OnDocumentDeleted(string documentPath);
    public void OnDocumentChanged(string documentPath);
    public void OnCommitChanges();
}