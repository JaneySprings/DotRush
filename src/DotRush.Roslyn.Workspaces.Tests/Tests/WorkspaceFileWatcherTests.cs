using DotRush.Roslyn.Workspaces.Extensions;
using DotRush.Roslyn.Workspaces.FileSystem;
using NUnit.Framework;

namespace DotRush.Roslyn.Workspaces.Tests;

public class WorkspaceFileWatcherTests : TestFixture {
    private TestWorkspace workspace = null!;
    private WorkspaceFilesWatcher filesWatcher = null!;
    private const int WatcherDelay = 2600;

    [SetUp]
    public void OnSetup() {
        workspace = new TestWorkspace();
        filesWatcher = new WorkspaceFilesWatcher(workspace);
    }
    [TearDown]
    public void OnTearDown() {
        filesWatcher.Dispose();
    }

    [Test]
    public async Task ObserveDocumentChangesTest() {
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");

        await workspace.LoadAsync(new[] { projectPath }, CancellationToken.None);
        filesWatcher.StartObserving(new[] { Path.GetDirectoryName(projectPath)! });
        await Task.Delay(500);
        
        var documentPath = CreateFileInProject(projectPath, "Program.cs", "class Program { static void Main() { } }");
        await Task.Delay(WatcherDelay);

        var documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Not.Empty);
        Assert.That(documentIds.Count(), Is.EqualTo(1));
        Assert.That(workspace.CreatedDocuments, Has.Count.EqualTo(1));
        Assert.That(workspace.UpdatedDocuments, Has.Count.EqualTo(0));
        Assert.That(workspace.DeletedDocuments, Has.Count.EqualTo(0));
        var document = workspace.Solution!.GetDocument(documentIds.Single());
        Assert.That(document!.FilePath, Is.EqualTo(documentPath));
        Assert.That(workspace.CreatedDocuments[0], Is.EqualTo(documentPath));

        File.WriteAllText(documentPath, "namespace MyProject; class Program { static void Main() { } }");
        await Task.Delay(WatcherDelay);

        documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Not.Empty);
        Assert.That(documentIds.Count(), Is.EqualTo(1));
        Assert.That(workspace.CreatedDocuments, Has.Count.EqualTo(1));
        Assert.That(workspace.UpdatedDocuments, Has.Count.EqualTo(1));
        Assert.That(workspace.DeletedDocuments, Has.Count.EqualTo(0));
        document = workspace.Solution!.GetDocument(documentIds.Single());
        Assert.That(document!.FilePath, Is.EqualTo(documentPath));
        Assert.That(workspace.UpdatedDocuments[0], Is.EqualTo(documentPath));
        var documentText = await document!.GetTextAsync();
        Assert.That(documentText.ToString(), Does.StartWith("namespace MyProject"));

        File.Delete(documentPath);
        await Task.Delay(WatcherDelay);
    
        documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Empty);
        Assert.That(workspace.CreatedDocuments, Has.Count.EqualTo(1));
        Assert.That(workspace.UpdatedDocuments, Has.Count.EqualTo(1));
        Assert.That(workspace.DeletedDocuments, Has.Count.EqualTo(1));
        Assert.That(workspace.DeletedDocuments[0], Is.EqualTo(documentPath));
    }
    [Test]
    public async Task ObserveFolderChangesTest() {
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");

        await workspace.LoadAsync(new[] { projectPath }, CancellationToken.None);
        filesWatcher.StartObserving(new[] { Path.GetDirectoryName(projectPath)! });
        await Task.Delay(500);
        
        CreateFileInProject(projectPath, Path.Combine("src", "Program1.cs"), "class Program { static void Main() { } }");
        CreateFileInProject(projectPath, Path.Combine("src", "Program2.cs"), "class Program2 { static void Main() { } }");
        CreateFileInProject(projectPath, Path.Combine("src", "Program3.cs"), "class Program3 { static void Main() { } }");
        await Task.Delay(WatcherDelay);

        Assert.That(workspace.CreatedDocuments, Has.Count.EqualTo(3));
        Assert.That(workspace.UpdatedDocuments, Has.Count.EqualTo(0));
        Assert.That(workspace.DeletedDocuments, Has.Count.EqualTo(0));
        for (int i = 1; i <= 3; i++) {
            var documentPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "src", $"Program{i}.cs");
            var documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
            Assert.That(documentIds, Is.Not.Empty);
            Assert.That(documentIds.Count(), Is.EqualTo(1));
            var document = workspace.Solution!.GetDocument(documentIds.Single());
            Assert.That(document!.FilePath, Is.EqualTo(documentPath));
            Assert.That(workspace.CreatedDocuments, Does.Contain(documentPath));
        }

        Directory.Delete(Path.Combine(Path.GetDirectoryName(projectPath)!, "src"), true);
        await Task.Delay(WatcherDelay);

        Assert.That(workspace.CreatedDocuments, Has.Count.EqualTo(3));
        Assert.That(workspace.UpdatedDocuments, Has.Count.EqualTo(0));
        Assert.That(workspace.DeletedDocuments, Has.Count.EqualTo(3));
        for (int i = 1; i <= 3; i++) {
            var documentPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "src", $"Program{i}.cs");
            var documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
            Assert.That(documentIds, Is.Empty);
            Assert.That(workspace.DeletedDocuments, Does.Contain(documentPath));
        }
    }
}