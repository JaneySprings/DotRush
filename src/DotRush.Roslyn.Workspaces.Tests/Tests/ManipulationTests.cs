using DotRush.Common.Extensions;
using DotRush.Roslyn.Workspaces.Extensions;
using NUnit.Framework;

namespace DotRush.Roslyn.Workspaces.Tests;

public class ManipulationTests : TestFixture {
    public ManipulationTests() {
        SafeExtensions.ThrowOnExceptions = true;
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

    private (string tfm1, string tfm2) GetTFMs(string tfm) {
        var tfms = tfm.Split(';');
        return (tfms[0], tfms[1]);
    }
}