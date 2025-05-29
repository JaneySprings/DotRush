using DotRush.Common.Extensions;
using DotRush.Roslyn.Workspaces.Extensions;
using NUnit.Framework;

namespace DotRush.Roslyn.Workspaces.Tests;

public class SimpleSolutionLoadTests : TestFixture {
    public SimpleSolutionLoadTests() {
        SafeExtensions.ThrowOnExceptions = true;
    }

    [Test]
    public async Task LoadSingleProjectTest() {
        var workspace = new TestWorkspace(restoreProjectsBeforeLoading: true);
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");
        var solutionPath = CreateSolution("MySolution", projectPath);
        var documentPath = CreateFileInProject(projectPath, "Program.cs", "class Program { static void Main() { } }");

        await workspace.LoadAsync(new[] { solutionPath }, CancellationToken.None);
        workspace.AssertLoadedProjects(1);
        Assert.That(workspace.Solution!.Projects.Count(), Is.EqualTo(1));
        Assert.That(workspace.Solution.Projects.First().Name, Is.EqualTo("MyProject"));

        var documentIds = workspace.Solution.GetDocumentIdsWithFilePathV2(documentPath);

        Assert.That(documentIds.Count(), Is.EqualTo(1));
        Assert.That(workspace.Solution.GetDocument(documentIds.First())!.Name, Is.EqualTo("Program.cs"));
    }
    [Test, Retry(3)]
    public async Task LoadSingleProjectsTest() {
        var workspace = new TestWorkspace();
        var project1Path = CreateProject("MyProject", SingleTFM, "Exe");
        var project2Path = CreateProject("MyProject2", SingleTFM, "Library");
        var solutionPath = CreateSolution("MySolution", project1Path, project2Path);

        await workspace.LoadAsync(new[] { solutionPath }, CancellationToken.None);
        workspace.AssertLoadedProjects(2);
        Assert.That(workspace.Solution!.Projects.Count(), Is.EqualTo(2));
        Assert.That(workspace.Solution.Projects.ElementAt(0).Name, Is.EqualTo("MyProject"));
        Assert.That(workspace.Solution.Projects.ElementAt(1).Name, Is.EqualTo("MyProject2"));
    }
    [Test]
    public async Task LoadSingleProjectsWithFilterTest() {
        var workspace = new TestWorkspace();
        var project1Path = CreateProject("MyProject", SingleTFM, "Exe");
        var project2Path = CreateProject("MyProject2", SingleTFM, "Library");
        var solutionPath = CreateSolution("MySolution", project1Path, project2Path);
        var solutionFilterPath = CreateSolutionFilter(solutionPath, project2Path);

        await workspace.LoadAsync(new[] { solutionFilterPath }, CancellationToken.None);
        workspace.AssertLoadedProjects(1);
        Assert.That(workspace.Solution!.Projects.Count(), Is.EqualTo(1));
        Assert.That(workspace.Solution.Projects.ElementAt(0).Name, Is.EqualTo("MyProject2"));
    }
    [Test]
    public async Task LoadSingleProjectsWithSolutionXTest() {
        var workspace = new TestWorkspace();
        var project1Path = CreateProject("MyProject", SingleTFM, "Exe");
        var project2Path = CreateProject("MyProject2", SingleTFM, "Library");
        var solutionPath = CreateSolutionX("MySolution", project1Path, project2Path);

        await workspace.LoadAsync(new[] { solutionPath }, CancellationToken.None);
        workspace.AssertLoadedProjects(2);
        Assert.That(workspace.Solution!.Projects.Count(), Is.EqualTo(2));
        Assert.That(workspace.Solution.Projects.ElementAt(0).Name, Is.EqualTo("MyProject"));
        Assert.That(workspace.Solution.Projects.ElementAt(1).Name, Is.EqualTo("MyProject2"));
    }
    [Test]
    public async Task LoadSingleProjectWithRelativePathTest() {
        var workspace = new TestWorkspace(restoreProjectsBeforeLoading: true);
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");
        var solutionPath = CreateSolution("MySolution", projectPath);
        var documentPath = CreateFileInProject(projectPath, "Program.cs", "class Program { static void Main() { } }");
        solutionPath = Path.GetRelativePath(Directory.GetCurrentDirectory(), solutionPath);

        await workspace.LoadAsync(new[] { solutionPath }, CancellationToken.None);
        workspace.AssertLoadedProjects(1);
        Assert.That(workspace.Solution!.Projects.Count(), Is.EqualTo(1));
        Assert.That(workspace.Solution.Projects.First().Name, Is.EqualTo("MyProject"));

        var documentIds = workspace.Solution.GetDocumentIdsWithFilePathV2(documentPath);

        Assert.That(documentIds.Count(), Is.EqualTo(1));
        Assert.That(workspace.Solution.GetDocument(documentIds.First())!.Name, Is.EqualTo("Program.cs"));
    }

    [Test]
    public async Task LoadMultitargetProjectTest() {
        var workspace = new TestWorkspace();
        var projectPath = CreateProject("MyProject", MultiTFM, "Exe");
        var solutionPath = CreateSolution("MySolution", projectPath);
        var documentPath = CreateFileInProject(projectPath, "Program.cs", "class Program { static void Main() { } }");

        await workspace.LoadAsync(new[] { solutionPath }, CancellationToken.None);
        workspace.AssertLoadedProjects(1);
        Assert.That(workspace.Solution!.Projects.Count(), Is.EqualTo(2));

        var documentIds = workspace.Solution.GetDocumentIdsWithFilePathV2(documentPath);
        Assert.That(documentIds.Count(), Is.EqualTo(2));

        var (tfm1, tfm2) = GetTFMs(MultiTFM);
        Assert.That(workspace.Solution.Projects.ElementAt(0).Name, Is.EqualTo($"MyProject({tfm1})"));
        Assert.That(workspace.Solution.Projects.ElementAt(1).Name, Is.EqualTo($"MyProject({tfm2})"));
    }
    [Test, Retry(3)]
    public async Task LoadMultitargetProjectsTest() {
        var workspace = new TestWorkspace();
        var project1Path = CreateProject("MyProject", MultiTFM, "Exe");
        var project2Path = CreateProject("MyProject2", MultiTFM, "Library");
        var solutionPath = CreateSolution("MySolution", project1Path, project2Path);

        await workspace.LoadAsync(new[] { solutionPath }, CancellationToken.None);
        workspace.AssertLoadedProjects(2);
        Assert.That(workspace.Solution!.Projects.Count(), Is.EqualTo(4));

        var (tfm1, tfm2) = GetTFMs(MultiTFM);
        Assert.That(workspace.Solution.Projects.ElementAt(0).Name, Is.EqualTo($"MyProject({tfm1})"));
        Assert.That(workspace.Solution.Projects.ElementAt(1).Name, Is.EqualTo($"MyProject({tfm2})"));
        Assert.That(workspace.Solution.Projects.ElementAt(2).Name, Is.EqualTo($"MyProject2({tfm1})"));
        Assert.That(workspace.Solution.Projects.ElementAt(3).Name, Is.EqualTo($"MyProject2({tfm2})"));
    }

    [Test]
    public async Task LoadSolutionAndProjectTest() {
        var workspace = new TestWorkspace();
        var project1Path = CreateProject("MyProject", SingleTFM, "Exe");
        var project2Path = CreateProject("MyProject2", SingleTFM, "Library");
        var solutionPath = CreateSolution("MySolution", project1Path);

        await workspace.LoadAsync(new[] { solutionPath, project2Path }, CancellationToken.None);
        workspace.AssertLoadedProjects(2);
        Assert.That(workspace.Solution!.Projects.Count(), Is.EqualTo(2));
        Assert.That(workspace.Solution.Projects.ElementAt(0).Name, Is.EqualTo("MyProject"));
        Assert.That(workspace.Solution.Projects.ElementAt(1).Name, Is.EqualTo("MyProject2"));
    }
    [Test]
    public async Task LoadSolutionsTest() {
        var workspace = new TestWorkspace();
        var project1Path = CreateProject("MyProject", SingleTFM, "Exe");
        var solution1Path = CreateSolution("MySolution", project1Path);
        var project2Path = CreateProject("MyProject2", SingleTFM, "Library");
        var solution2Path = CreateSolution("MySolution2", project2Path);

        await workspace.LoadAsync(new[] { solution1Path, solution2Path }, CancellationToken.None);
        workspace.AssertLoadedProjects(2);
        Assert.That(workspace.Solution!.Projects.Count(), Is.EqualTo(2));
        Assert.That(workspace.Solution.Projects.ElementAt(0).Name, Is.EqualTo("MyProject"));
        Assert.That(workspace.Solution.Projects.ElementAt(1).Name, Is.EqualTo("MyProject2"));
    }

    [Test]
    public void ErrorOnRestoreTest() {
        var workspace = new TestWorkspace(restoreProjectsBeforeLoading: true);
        var projectPath = CreateProject("MyProject", "MyError>/<", "Exe");
        var solutionPath = CreateSolution("MySolution", projectPath);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await workspace.LoadAsync(new[] { projectPath }, CancellationToken.None));
        Assert.That(workspace.Solution, Is.Null);
    }
    [Test, Retry(3)]
    public async Task GlobalWorkspacePropertiesTest() {
        var (tfm1, tfm2) = GetTFMs(MultiTFM);
        var workspace = new TestWorkspace(new Dictionary<string, string> { { "TargetFramework", tfm1 } });
        var project1Path = CreateProject("MyProject", MultiTFM, "Exe");
        var project2Path = CreateProject("MyProject2", MultiTFM, "Library");
        var solutionPath = CreateSolution("MySolution", project1Path, project2Path);

        await workspace.LoadAsync(new[] { solutionPath }, CancellationToken.None);
        workspace.AssertLoadedProjects(2);
        Assert.That(workspace.Solution!.Projects.Count(), Is.EqualTo(2));

        Assert.That(workspace.Solution.Projects.ElementAt(0).Name, Is.EqualTo("MyProject"));
        Assert.That(workspace.Solution.Projects.ElementAt(1).Name, Is.EqualTo("MyProject2"));
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