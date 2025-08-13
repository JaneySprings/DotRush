using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class WorkspaceExtensionsTests : MultitargetProjectFixture {
    [Test]
    public void IntermediateDirectoriesTest() {
        var projectPath = Workspace.Solution!.Projects.First().FilePath;
        foreach (var project in Workspace.Solution.Projects) {
            var tfm = project.GetTargetFramework();
            Assert.That(project.GetProjectDirectory(), Is.EqualTo(Path.GetDirectoryName(projectPath)!));
            Assert.That(project.GetIntermediateOutputPath(), Is.EqualTo(Path.Combine(Path.GetDirectoryName(projectPath)!, "obj", "Debug", tfm)));
            Assert.That(project.GetOutputPath(), Is.EqualTo(Path.Combine(Path.GetDirectoryName(projectPath)!, "bin", "Debug")));
        }
    }

    [TestCase("FoldersTest.cs")]
    [TestCase("Folder1", "FoldersTest.cs")]
    [TestCase("Folder1", "Fold er2", "FoldersTest.cs")]
    [TestCase("_Foler1", "Fold er2", "Folder3", "FoldersTest.cs")]
    public void DocumentFoldersTest(params string[] parts) {
        var documents = CreateAndGetDocuments(parts, "public class TestFile1 {}");
        Assert.That(documents, Has.Length.EqualTo(2));
        foreach (var document in documents) {
            Assert.That(document.Folders, Has.Count.EqualTo(parts.Length - 1));
            for (int i = 0; i < document.Folders.Count; i++)
                Assert.That(document.Folders[i], Is.EqualTo(parts[i]));
        }
    }

    [Test]
    public void ConditionalDocumentIncludeTest() {
        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(2), "Expected two projects in the solution, one for each target framework.");
        var expectedProject = Workspace.Solution.Projects.First();
        var intermediatePath = expectedProject.GetIntermediateOutputPath(); // obj/Debug/netX.0
        var documentPath = Path.Combine(intermediatePath, $"{nameof(WorkspaceExtensionsTests)}.cs");
        var projectIds = Workspace.Solution.GetProjectIdsMayContainsFilePath(documentPath);

        Assert.That(projectIds.Count(), Is.EqualTo(1));
        Assert.That(projectIds.First(), Is.EqualTo(expectedProject.Id), $"ProjectId is not {expectedProject.Name}");
    }


    private Document[] CreateAndGetDocuments(string[] parts, string content) {
        if (parts.Length == 0)
            throw new ArgumentException("At least one part is required to create a document.");

        var documentPath = Path.Combine(ProjectDirectory, Path.Combine(parts));
        var documentDirectory = Path.GetDirectoryName(documentPath)!;

        if (!Directory.Exists(documentDirectory))
            Directory.CreateDirectory(documentDirectory);
        if (File.Exists(documentPath))
            throw new InvalidOperationException($"Document '{documentPath}' already exists.");

        File.WriteAllText(documentPath, content);
        Workspace.CreateDocument(documentPath);
        return Workspace.Solution!.GetDocumentIdsWithFilePath(documentPath).Select(id => Workspace.Solution.GetDocument(id)).ToArray()!;
    }
}