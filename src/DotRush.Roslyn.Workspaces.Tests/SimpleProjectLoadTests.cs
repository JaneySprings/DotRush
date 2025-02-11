using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Workspaces.Extensions;
using NUnit.Framework;

namespace DotRush.Roslyn.Workspaces.Tests;

public class SimpleProjectLoadTests : TestFixture {
    private const string SingleTFM = "net8.0";
    private const string MultiTFM = "net8.0;net9.0";

    public SimpleProjectLoadTests() {
        SafeExtensions.ThrowOnExceptions = true;
    }

    [Test]
    public void LoadSimpleProjectTest() {
        var workspace = new TestWorkspace();
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");
        var documentPath = CreateFileInProject(projectPath, "Program.cs", "class Program { static void Main() { } }");

        workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).Wait();
        Assert.That(workspace.Solution!.Projects.Count(), Is.EqualTo(1));
        Assert.That(workspace.Solution.Projects.First().Name, Is.EqualTo("MyProject"));

        var documentIds = workspace.Solution.GetDocumentIdsWithFilePathV2(documentPath);

        Assert.That(documentIds.Count(), Is.EqualTo(1));
        Assert.That(workspace.Solution.GetDocument(documentIds.First())!.Name, Is.EqualTo("Program.cs"));
    }

    [Test]
    public void LoadMultitargetProjectTest() {
        var workspace = new TestWorkspace();
        var projectPath = CreateProject("MyProject", MultiTFM, "Exe");
        var documentPath = CreateFileInProject(projectPath, "Program.cs", "class Program { static void Main() { } }");

        workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).Wait();
        Assert.That(workspace.Solution!.Projects.Count(), Is.EqualTo(2));

        var documentIds = workspace.Solution.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds.Count(), Is.EqualTo(2));

        foreach (var tfm in MultiTFM.Split(';')) {
            Assert.That(workspace.Solution.Projects.Any(p => p.Name == $"MyProject({tfm})"), Is.True, $"Loaded project with '{tfm}' not found");
        }
    }
}