using System.Xml;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class SimpleSolutionsLoadTests : SimpleWorkspaceFixture {
    private const string SingleTFM = "net8.0";
    private const string MultiTFM = "net8.0;net10.0";

    [Test]
    public async Task LoadSingleProjectTest() {
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");
        var solutionPath = CreateSolution("MySolution", projectPath);

        await Workspace.LoadAsync(new[] { solutionPath }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(1));
        Assert.That(Workspace.Solution.Projects.First().Name, Is.EqualTo("MyProject"));
    }
    [Test]
    public async Task LoadSingleProjectsTest() {
        var project1Path = CreateProject("MyProject", SingleTFM, "Exe");
        var project2Path = CreateProject("MyProject2", SingleTFM, "Library");
        var solutionPath = CreateSolution("MySolution", project1Path, project2Path);

        await Workspace.LoadAsync(new[] { solutionPath }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(2));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == "MyProject2"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == "MyProject"));
    }
    [Test]
    public async Task LoadSingleProjectsWithFilterTest() {
        var project1Path = CreateProject("MyProject", SingleTFM, "Exe");
        var project2Path = CreateProject("MyProject2", SingleTFM, "Library");
        var solutionPath = CreateSolution("MySolution", project1Path, project2Path);
        var solutionFilterPath = CreateSolutionFilter(solutionPath, project2Path);

        await Workspace.LoadAsync(new[] { solutionFilterPath }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(1));
        Assert.That(Workspace.Solution.Projects.First().Name, Is.EqualTo("MyProject2"));
    }
    [Test]
    public async Task LoadSingleProjectsWithSolutionXTest() {
        var project1Path = CreateProject("MyProject", SingleTFM, "Exe");
        var project2Path = CreateProject("MyProject2", SingleTFM, "Library");
        var solutionPath = CreateSolutionX("MySolution", project1Path, project2Path);

        await Workspace.LoadAsync(new[] { solutionPath }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(2));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == "MyProject2"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == "MyProject"));
    }
    [Test]
    public async Task LoadSingleProjectWithRelativePathTest() {
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");
        var solutionPath = CreateSolution("MySolution", projectPath);
        solutionPath = Path.GetRelativePath(Directory.GetCurrentDirectory(), solutionPath);

        await Workspace.LoadAsync(new[] { solutionPath }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(1));
        Assert.That(Workspace.Solution.Projects.First().Name, Is.EqualTo("MyProject"));
    }

    [Test]
    public async Task LoadMultitargetProjectTest() {
        var projectPath = CreateProject("MyProject", MultiTFM, "Exe");
        var solutionPath = CreateSolution("MySolution", projectPath);

        await Workspace.LoadAsync(new[] { solutionPath }, CancellationToken.None).ConfigureAwait(false);
        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(2));

        var (tfm1, tfm2) = GetTFMs(MultiTFM);
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject({tfm1})"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject({tfm2})"));
    }
    [Test]
    public async Task LoadMultitargetProjectsTest() {
        var project1Path = CreateProject("MyProject", MultiTFM, "Exe");
        var project2Path = CreateProject("MyProject2", MultiTFM, "Library");
        var solutionPath = CreateSolution("MySolution", project1Path, project2Path);

        await Workspace.LoadAsync(new[] { solutionPath }, CancellationToken.None).ConfigureAwait(false);
        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(4));

        var (tfm1, tfm2) = GetTFMs(MultiTFM);
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject2({tfm1})"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject2({tfm2})"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject({tfm1})"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject({tfm2})"));
    }

    [Test]
    public async Task LoadSolutionAndProjectTest() {
        var project1Path = CreateProject("MyProject", SingleTFM, "Exe");
        var project2Path = CreateProject("MyProject2", SingleTFM, "Library");
        var solutionPath = CreateSolution("MySolution", project1Path);

        await Workspace.LoadAsync(new[] { solutionPath, project2Path }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(2));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == "MyProject"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == "MyProject2"));
    }
    [Test]
    public async Task LoadSolutionsTest() {
        var project1Path = CreateProject("MyProject", SingleTFM, "Exe");
        var solution1Path = CreateSolution("MySolution", project1Path);
        var project2Path = CreateProject("MyProject2", SingleTFM, "Library");
        var solution2Path = CreateSolution("MySolution2", project2Path);

        await Workspace.LoadAsync(new[] { solution1Path, solution2Path }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(2));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == "MyProject"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == "MyProject2"));
    }

    [Test]
    public void ErrorOnRestoreTest() {
        var projectPath = CreateProject("MyProject", "MyError>/<", "Exe");
        var solutionPath = CreateSolution("MySolution", projectPath);

        Assert.ThrowsAsync<XmlException>(async () => await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false));
        Assert.That(Workspace.Solution, Is.Null);
    }
    // [Test] // https://github.com/dotnet/msbuild/issues/10266
    // public void CheckMSBuildWorkspaceSlnxSupportTest() {
    //     var projectPath = CreateProject("MyProject2", MultiTFM, "Library");
    //     var solutionPath = CreateSolutionX("MySolution", projectPath);
    //     var workspace = new TestWorkspace();
    //     Assert.ThrowsAsync<Microsoft.Build.Exceptions.InvalidProjectFileException>(async () => await workspace.LoadAsync(new[] { solutionPath }, CancellationToken.None));
    // }


    private (string tfm1, string tfm2) GetTFMs(string tfm) {
        var tfms = tfm.Split(';');
        return (tfms[0], tfms[1]);
    }
}