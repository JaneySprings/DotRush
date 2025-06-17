using DotRush.Common.Extensions;
using DotRush.Roslyn.Workspaces.Extensions;
using NUnit.Framework;

namespace DotRush.Roslyn.Workspaces.Tests;

public class CreateUpdateDeleteTests : TestFixture {
    public CreateUpdateDeleteTests() {
        SafeExtensions.ThrowOnExceptions = true;
    }

    [Test]
    public async Task DocumentChangesInSingleProjectTest() {
        var workspace = new TestWorkspace();
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");

        await workspace.LoadAsync(new[] { projectPath }, CancellationToken.None);

        var documentPath = CreateFileInProject(projectPath, "Program.cs", "class Program { static void Main() { } }");
        var documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Empty);
        workspace.CreateDocument(documentPath);
        documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Not.Empty);
        Assert.That(documentIds.Count(), Is.EqualTo(1));
        var document = workspace.Solution!.GetDocument(documentIds.Single());
        Assert.That(document!.FilePath, Is.EqualTo(documentPath));

        FileSystemExtensions.WriteAllText(documentPath, "namespace MyProject; class Program { static void Main() { } }");
        var documentText = await document!.GetTextAsync();
        Assert.That(documentText.ToString(), Does.StartWith("class Program"));
        workspace.UpdateDocument(documentPath);
        documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Not.Empty);
        Assert.That(documentIds.Count(), Is.EqualTo(1));
        document = workspace.Solution!.GetDocument(documentIds.Single());
        Assert.That(document!.FilePath, Is.EqualTo(documentPath));
        documentText = await document!.GetTextAsync();
        Assert.That(documentText.ToString(), Does.StartWith("namespace MyProject"));

        File.Delete(documentPath);
        workspace.DeleteDocument(documentPath);
        documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Empty);
    }
    [Test]
    public async Task DocumentChangesInMultitargetProjectTest() {
        var workspace = new TestWorkspace();
        var projectPath = CreateProject("MyProject", MultiTFM, "Exe");

        await workspace.LoadAsync(new[] { projectPath }, CancellationToken.None);

        var documentPath = CreateFileInProject(projectPath, "Program.cs", "class Program { static void Main() { } }");
        var documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Empty);
        workspace.CreateDocument(documentPath);
        documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Not.Empty);
        Assert.That(documentIds.Count(), Is.EqualTo(2));
        foreach (var documentId in documentIds) {
            Assert.That(workspace.Solution!.GetDocument(documentId)!.FilePath, Is.EqualTo(documentPath));
        }

        FileSystemExtensions.WriteAllText(documentPath, "namespace MyProject; class Program { static void Main() { } }");
        foreach (var documentId in documentIds) {
            var document = workspace.Solution!.GetDocument(documentId);
            var documentText = await document!.GetTextAsync();
            Assert.That(documentText.ToString(), Does.StartWith("class Program"));
        }
        workspace.UpdateDocument(documentPath);
        documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Not.Empty);
        Assert.That(documentIds.Count(), Is.EqualTo(2));
        foreach (var documentId in documentIds) {
            var document = workspace.Solution!.GetDocument(documentId);
            var documentText = await document!.GetTextAsync();
            Assert.That(documentText.ToString(), Does.StartWith("namespace MyProject"));
        }

        File.Delete(documentPath);
        workspace.DeleteDocument(documentPath);
        documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Empty);
    }

    [Test]
    public async Task CreateOneDocumentMultipleTimesTest() {
        var workspace = new TestWorkspace();
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");

        await workspace.LoadAsync(new[] { projectPath }, CancellationToken.None);

        var oldProjectFilesCount = GetProjectDocumentsCount(workspace.Solution!.Projects.Single());
        var newProjectFilesCount = oldProjectFilesCount;

        var documentPath = CreateFileInProject(projectPath, "Program.cs", "class Program { static void Main() { } }");
        var documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Empty);

        workspace.CreateDocument(documentPath);
        oldProjectFilesCount++;
        newProjectFilesCount = GetProjectDocumentsCount(workspace.Solution!.Projects.Single());

        documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Not.Empty);
        Assert.That(documentIds.Count(), Is.EqualTo(1));
        var document = workspace.Solution!.GetDocument(documentIds.Single());
        Assert.That(document!.FilePath, Is.EqualTo(documentPath));
        Assert.That(newProjectFilesCount, Is.EqualTo(oldProjectFilesCount));

        workspace.CreateDocument(documentPath);
        newProjectFilesCount = GetProjectDocumentsCount(workspace.Solution!.Projects.Single());

        documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Not.Empty);
        Assert.That(documentIds.Count(), Is.EqualTo(1));
        document = workspace.Solution!.GetDocument(documentIds.Single());
        Assert.That(document!.FilePath, Is.EqualTo(documentPath));
        Assert.That(newProjectFilesCount, Is.EqualTo(oldProjectFilesCount));
    }
    [Test]
    public async Task DeleteOneDocumentMultipleTimesTest() {
        var workspace = new TestWorkspace();
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");
        var documentPath = CreateFileInProject(projectPath, "Program.cs", "class Program { static void Main() { } }");

        await workspace.LoadAsync(new[] { projectPath }, CancellationToken.None);

        var documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Not.Empty);

        workspace.DeleteDocument(documentPath);
        documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Empty);

        workspace.DeleteDocument(documentPath);
        documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Empty);
    }
    [Test]
    public async Task SkipCompilerGeneratedFilesTest() {
        var workspace = new TestWorkspace();
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");
        await workspace.LoadAsync(new[] { projectPath }, CancellationToken.None);

        var documentPath = CreateFileInProject(projectPath, "Program.g.cs", "class Program { static void Main() { } }");
        workspace.CreateDocument(documentPath);

        var documentIds = workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds, Is.Empty);
    }
}