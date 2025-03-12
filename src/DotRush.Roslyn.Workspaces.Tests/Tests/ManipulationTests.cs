using DotRush.Common.Extensions;
using DotRush.Roslyn.Workspaces.Extensions;
using NUnit.Framework;

namespace DotRush.Roslyn.Workspaces.Tests;

public class ManipulationTests : TestFixture {
    public ManipulationTests() {
        SafeExtensions.ThrowOnExceptions = true;
    }

    [Test]
    public async Task IntermediateDirectoriesTest() {
        var workspace = new TestWorkspace();
        var projectPath = CreateProject("MyProject", MultiTFM, "Exe");

        await workspace.LoadAsync(new[] { projectPath }, CancellationToken.None);
        foreach (var project in workspace.Solution!.Projects) {
            var tfm = project.GetTargetFramework();
            Assert.That(project.GetProjectDirectory(), Is.EqualTo(Path.GetDirectoryName(projectPath)!));
            Assert.That(project.GetIntermediateOutputPath(), Is.EqualTo(Path.Combine(Path.GetDirectoryName(projectPath)!, "obj", "Debug", tfm)));
            Assert.That(project.GetOutputPath(), Is.EqualTo(Path.Combine(Path.GetDirectoryName(projectPath)!, "bin", "Debug", tfm)));
        }
    }
    [Test]
    public async Task DocumentFoldersTest() {
        var workspace = new TestWorkspace();
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");

        await workspace.LoadAsync(new[] { projectPath }, CancellationToken.None);
        
        var document1Path = CreateFileInProject(projectPath, "Document1.cs", "class Document1 {}");
        workspace.CreateDocument(document1Path);
        var document1 = workspace.Solution!.GetDocument(workspace.Solution!.GetDocumentIdsWithFilePathV2(document1Path).Single())!;
        Assert.That(document1.FilePath, Is.EqualTo(document1Path));
        Assert.That(document1.Folders, Is.Empty);

        var document2Path = CreateFileInProject(projectPath, "Folder1/Document2.cs", "class Document2 {}");
        workspace.CreateDocument(document2Path);
        var document2 = workspace.Solution!.GetDocument(workspace.Solution!.GetDocumentIdsWithFilePathV2(document2Path).Single())!;
        Assert.That(document2.FilePath, Is.EqualTo(document2Path));
        Assert.That(document2.Folders, Is.Not.Empty);
        Assert.That(document2.Folders, Has.Count.EqualTo(1));
        Assert.That(document2.Folders.ElementAt(0), Is.EqualTo("Folder1"));

        var document3Path = CreateFileInProject(projectPath, "Folder1/Folder2/Document3.cs", "class Document3 {}");
        workspace.CreateDocument(document3Path);
        var document3 = workspace.Solution!.GetDocument(workspace.Solution!.GetDocumentIdsWithFilePathV2(document3Path).Single())!;
        Assert.That(document3.FilePath, Is.EqualTo(document3Path));
        Assert.That(document3.Folders, Is.Not.Empty);
        Assert.That(document3.Folders, Has.Count.EqualTo(2));
        Assert.That(document3.Folders.ElementAt(0), Is.EqualTo("Folder1"));
        Assert.That(document3.Folders.ElementAt(1), Is.EqualTo("Folder2"));

    }
    [Test]
    public async Task ProjectConditionalIncludeTest() {
        var (tfm1, tfm2) = GetTFMs(MultiTFM);
        var workspace = new TestWorkspace();

        var project1Path = CreateProject("MyProject1", "MultiProject", tfm1, "Exe");
        var document1Path = CreateFileInProject(project1Path, "Platforms/Windows/Document1.cs", "class Document1 {}");
        await workspace.LoadAsync(new[] { project1Path }, CancellationToken.None);

        var project2Path = CreateProject("MyProject2", "MultiProject", tfm2, "Exe");
        var document2Path = CreateFileInProject(project2Path, "Platforms/Linux/Document2.cs", "class Document2 {}");
        await workspace.LoadAsync(new[] { project2Path }, CancellationToken.None);

        Assert.That(workspace.Solution!.Projects.Count(), Is.EqualTo(2));
        var project1 = workspace.Solution!.Projects.ElementAt(0);
        var project2 = workspace.Solution!.Projects.ElementAt(1);
        //Remove document1 form project2 after loading (test only)
        var _upd = project2.RemoveDocument(project2.GetDocumentIdsWithFilePath(document1Path).Single());
        workspace.SetSolution(_upd.Solution);
        project1 = workspace.Solution!.Projects.ElementAt(0);
        project2 = workspace.Solution!.Projects.ElementAt(1);
        // (test only)

        Assert.That(project1.GetDocumentIdsWithFilePath(document1Path).Count(), Is.EqualTo(1));
        Assert.That(project1.GetDocumentIdsWithFilePath(document2Path), Is.Empty);
        Assert.That(project2.GetDocumentIdsWithFilePath(document2Path).Count(), Is.EqualTo(1));
        Assert.That(project2.GetDocumentIdsWithFilePath(document1Path), Is.Empty);

        var document3Path = CreateFileInProject(project1Path, "Platforms/Document3.cs", "class Document3 {}");
        workspace.CreateDocument(document3Path);
        project1 = workspace.Solution!.Projects.ElementAt(0);
        project2 = workspace.Solution!.Projects.ElementAt(1);
        Assert.That(project1.GetDocumentIdsWithFilePath(document3Path).Count(), Is.EqualTo(1));
        Assert.That(project2.GetDocumentIdsWithFilePath(document3Path).Count(), Is.EqualTo(1));

        var document4Path = CreateFileInProject(project1Path, "Platforms/Windows/Document4.cs", "class Document4 {}");
        workspace.CreateDocument(document4Path);
        project1 = workspace.Solution!.Projects.ElementAt(0);
        project2 = workspace.Solution!.Projects.ElementAt(1);
        Assert.That(project1.GetDocumentIdsWithFilePath(document4Path).Count(), Is.EqualTo(1));
        Assert.That(project2.GetDocumentIdsWithFilePath(document4Path), Is.Empty);

        var document5Path = CreateFileInProject(project1Path, "Platforms/Linux/Document5.cs", "class Document5 {}");
        workspace.CreateDocument(document5Path);
        project1 = workspace.Solution!.Projects.ElementAt(0);
        project2 = workspace.Solution!.Projects.ElementAt(1);
        Assert.That(project1.GetDocumentIdsWithFilePath(document5Path), Is.Empty);
        Assert.That(project2.GetDocumentIdsWithFilePath(document5Path).Count(), Is.EqualTo(1));
    }
    [Test]
    public async Task NoSyncInIntermediateDirectoryTest() {
        var workspace = new TestWorkspace();
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");

        await workspace.LoadAsync(new[] { projectPath }, CancellationToken.None);
        
        var documentPath = CreateFileInProject(projectPath, $"obj/Debug/{SingleTFM}/Document.cs", "class Document {}");
        workspace.CreateDocument(documentPath);
        Assert.That(workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath), Is.Empty);

        var objDocument = workspace.Solution!.Projects.Single().Documents.First(d => d.FilePath!.Contains($"obj{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}{SingleTFM}"));
        workspace.DeleteDocument(objDocument.FilePath!);
        Assert.That(workspace.Solution!.GetDocumentIdsWithFilePathV2(objDocument.FilePath!), Is.Not.Empty);

        var document2Path = CreateFileInProject(projectPath, $"objects/Document2.cs", "class Document2 {}");
        workspace.CreateDocument(document2Path);
        Assert.That(workspace.Solution!.GetDocumentIdsWithFilePathV2(document2Path), Is.Not.Empty);
    }

    private (string tfm1, string tfm2) GetTFMs(string tfm) {
        var tfms = tfm.Split(';');
        return (tfms[0], tfms[1]);
    }
}