namespace DotRush.Roslyn.Workspaces.FileSystem;

public interface IWorkspaceChangeListener {
    /// <summary>Files created within a short time window are reported together, so a project is evaluated once per batch.</summary>
    public void OnDocumentsCreated(IReadOnlyList<string> documentPaths);
    public void OnDocumentDeleted(string documentPath);
    public void OnDocumentChanged(string documentPath);
}
