using DotRush.Common.MSBuild;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class MSBuildSolutionLoaderTests : SimpleWorkspaceFixture {
    [Test]
    public void GetProjectsFromClassicSolutionTest() {
        var project1 = CreateProject("Project1");
        var project2 = CreateProject("Project2");
        var solutionPath = CreateSolution("TestSolution", project1, project2);

        var projects = MSBuildSolutionLoader.GetProjectFiles(solutionPath);

        Assert.That(projects, Is.Not.Null);
        Assert.That(projects, Has.Count.EqualTo(2));
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project1)));
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project2)));
    }
    [Test]
    public void GetProjectsFromSolutionXTest() {
        var project1 = CreateProject("Project1");
        var project2 = CreateProject("Project2");
        var solutionPath = CreateSolutionX("TestSolution", project1, project2);

        var projects = MSBuildSolutionLoader.GetProjectFiles(solutionPath);

        Assert.That(projects, Is.Not.Null);
        Assert.That(projects, Has.Count.EqualTo(2));
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project1)));
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project2)));
    }
    [Test]
    public void GetProjectsFromSolutionFilterTest() {
        var project1 = CreateProject("Project1");
        var project2 = CreateProject("Project2");
        var project3 = CreateProject("Project3");
        var solutionPath = CreateSolution("TestSolution", project1, project2, project3);
        var filterPath = CreateSolutionFilter(solutionPath, project1, project2);

        var projects = MSBuildSolutionLoader.GetProjectFiles(filterPath);

        Assert.That(projects, Is.Not.Null);
        Assert.That(projects, Has.Count.EqualTo(2));
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project1)));
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project2)));
    }
    [Test]
    public void GetProjectsFromEmptySolutionTest() {
        var solutionPath = CreateSolution("EmptySolution");

        var projects = MSBuildSolutionLoader.GetProjectFiles(solutionPath);

        Assert.That(projects, Is.Not.Null);
        Assert.That(projects, Is.Empty);
    }
    [Test]
    public void GetProjectsWithInvalidSolutionPathTest() {
        var invalidPath = Path.Combine(SandboxDirectory, "NonExistent.sln");

        var projects = MSBuildSolutionLoader.GetProjectFiles(invalidPath);

        Assert.That(projects, Is.Empty);
    }
    [Test]
    public void GetProjectsWithUnsupportedExtensionTest() {
        var unsupportedPath = Path.Combine(SandboxDirectory, "Unsupported.txt");
        File.WriteAllText(unsupportedPath, "This is not a solution file");

        var projects = MSBuildSolutionLoader.GetProjectFiles(unsupportedPath);

        Assert.That(projects, Is.Empty);
    }
    [Test]
    public void GetProjectsEmptySolutionXTest() {
        var emptySolutionPath = Path.Combine(SandboxDirectory, "EmptySolution.slnx");
        File.WriteAllText(emptySolutionPath, "<Solution></Solution>");

        var projects = MSBuildSolutionLoader.GetProjectFiles(emptySolutionPath);

        Assert.That(projects, Is.Empty);
    }
    [Test]
    public void GetProjectsInvalidSolutionFilterTest() {
        var invalidFilterPath = Path.Combine(SandboxDirectory, "Invalid.slnf");
        File.WriteAllText(invalidFilterPath, "{ \"invalid\": true }");

        var projects = MSBuildSolutionLoader.GetProjectFiles(invalidFilterPath);

        Assert.That(projects, Is.Empty);
    }
    [Test]
    public void GetProjectsSkipsSolutionFoldersTest() {
        var project1 = CreateProject("Project1");
        var project2 = CreateProject("Project2");
        var solutionPath = Path.Combine(SandboxDirectory, "SolutionWithFolder.sln");
        var solutionContent = @"Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""Project1"", """ + project1 + @""", ""{" + Guid.NewGuid() + @"}""
EndProject
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""Project2"", """ + project2 + @""", ""{" + Guid.NewGuid() + @"}""
EndProject
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""SolutionFolder"", ""SolutionFolder"", ""{" + Guid.NewGuid() + @"}""
EndProject
Global
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal";
        File.WriteAllText(solutionPath, solutionContent);

        var projects = MSBuildSolutionLoader.GetProjectFiles(solutionPath);

        Assert.That(projects, Is.Not.Null);
        Assert.That(projects, Has.Count.EqualTo(2), "Solution folders should be skipped");
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project1)));
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project2)));
    }
}