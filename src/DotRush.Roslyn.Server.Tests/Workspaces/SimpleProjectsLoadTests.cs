using System.Xml;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class SimpleProjectsLoadTests : SimpleWorkspaceFixture {
    private const string SingleTFM = "net8.0";
    private const string MultiTFM = "net8.0;net10.0";

    [Test]
    public async Task LoadSingleProjectTest() {
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");

        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(1));
        Assert.That(Workspace.Solution.Projects.First().Name, Is.EqualTo("MyProject"));
    }
    [Test]
    public async Task LoadSingleProjectsTest() {
        var project1Path = CreateProject("MyProject", SingleTFM, "Exe");
        var project2Path = CreateProject("MyProject2", SingleTFM, "Library");

        await Workspace.LoadAsync(new[] { project2Path, project1Path }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(2));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == "MyProject2"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == "MyProject"));
    }
    [Test]
    public async Task LoadSingleProjectWithRelativePathTest() {
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");
        projectPath = Path.GetRelativePath(Directory.GetCurrentDirectory(), projectPath);

        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false);
        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(1));
        Assert.That(Workspace.Solution.Projects.First().Name, Is.EqualTo("MyProject"));
    }

    [Test]
    public async Task LoadMultitargetProjectTest() {
        var projectPath = CreateProject("MyProject", MultiTFM, "Exe");

        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false);
        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(2));

        var (tfm1, tfm2) = GetTFMs(MultiTFM);
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject({tfm1})"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject({tfm2})"));
    }
    [Test]
    public async Task LoadMultitargetProjectsTest() {
        var project1Path = CreateProject("MyProject", MultiTFM, "Exe");
        var project2Path = CreateProject("MyProject2", MultiTFM, "Library");

        await Workspace.LoadAsync(new[] { project2Path, project1Path }, CancellationToken.None).ConfigureAwait(false);
        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(4));

        var (tfm1, tfm2) = GetTFMs(MultiTFM);
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject2({tfm1})"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject2({tfm2})"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject({tfm1})"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject({tfm2})"));
    }

    [Test]
    public void ErrorOnRestoreTest() {
        var projectPath = CreateProject("MyProject", "MyError>/<", "Exe");

        Assert.ThrowsAsync<XmlException>(async () => await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false));
        Assert.That(Workspace.Solution, Is.Null);
    }

    private (string tfm1, string tfm2) GetTFMs(string tfm) {
        var tfms = tfm.Split(';');
        return (tfms[0], tfms[1]);
    }
}