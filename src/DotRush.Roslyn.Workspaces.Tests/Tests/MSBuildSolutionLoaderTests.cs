using DotRush.Common.MSBuild;
using NUnit.Framework;

namespace DotRush.Roslyn.Workspaces.Tests;

public class MSBuildSolutionLoaderTests : TestFixture {
    [Test]
    public void GetProjectsFromClassicSolutionTest() {
        // Arrange
        var project1 = CreateProject("Project1", SingleTFM, "Library");
        var project2 = CreateProject("Project2", SingleTFM, "Library");
        var solutionPath = CreateSolution("TestSolution", project1, project2);

        // Act
        var projects = MSBuildSolutionLoader.GetProjectFiles(solutionPath);

        // Assert
        Assert.That(projects, Is.Not.Null);
        Assert.That(projects, Has.Count.EqualTo(2));
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project1)));
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project2)));
    }

    [Test]
    public void GetProjectsFromSolutionXTest() {
        // Arrange
        var project1 = CreateProject("Project1", SingleTFM, "Library");
        var project2 = CreateProject("Project2", SingleTFM, "Library");
        var solutionPath = CreateSolutionX("TestSolution", project1, project2);

        // Act
        var projects = MSBuildSolutionLoader.GetProjectFiles(solutionPath);

        // Assert
        Assert.That(projects, Is.Not.Null);
        Assert.That(projects, Has.Count.EqualTo(2));
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project1)));
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project2)));
    }

    [Test]
    public void GetProjectsFromSolutionFilterTest() {
        // Arrange
        var project1 = CreateProject("Project1", SingleTFM, "Library");
        var project2 = CreateProject("Project2", SingleTFM, "Library");
        var solutionPath = CreateSolution("TestSolution", project1, project2);
        var filterPath = CreateSolutionFilter(solutionPath, project1, project2);

        // Act
        var projects = MSBuildSolutionLoader.GetProjectFiles(filterPath);

        // Assert
        Assert.That(projects, Is.Not.Null);
        Assert.That(projects, Has.Count.EqualTo(2));
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project1)));
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project2)));
    }

    [Test]
    public void GetProjectsFromEmptySolutionTest() {
        // Arrange
        var solutionPath = CreateSolution("EmptySolution");

        // Act
        var projects = MSBuildSolutionLoader.GetProjectFiles(solutionPath);

        // Assert
        Assert.That(projects, Is.Not.Null);
        Assert.That(projects, Is.Empty);
    }

    [Test]
    public void GetProjectsWithInvalidSolutionPathTest() {
        // Arrange
        var invalidPath = Path.Combine(SandboxDirectory, "NonExistent.sln");

        // Act
        var projects = MSBuildSolutionLoader.GetProjectFiles(invalidPath);

        // Assert
        Assert.That(projects, Is.Empty);
    }

    [Test]
    public void GetProjectsWithUnsupportedExtensionTest() {
        // Arrange
        var unsupportedPath = Path.Combine(SandboxDirectory, "Unsupported.txt");
        File.WriteAllText(unsupportedPath, "This is not a solution file");

        // Act
        var projects = MSBuildSolutionLoader.GetProjectFiles(unsupportedPath);

        // Assert
        Assert.That(projects, Is.Empty);
    }

    [Test]
    public void GetProjectsEmptySolutionXTest() {
        // Arrange
        var emptySolutionPath = Path.Combine(SandboxDirectory, "EmptySolution.slnx");
        File.WriteAllText(emptySolutionPath, "<Solution></Solution>");

        // Act
        var projects = MSBuildSolutionLoader.GetProjectFiles(emptySolutionPath);

        // Assert
        Assert.That(projects, Is.Empty);
    }

    [Test]
    public void GetProjectsInvalidSolutionFilterTest() {
        // Arrange
        var invalidFilterPath = Path.Combine(SandboxDirectory, "Invalid.slnf");
        File.WriteAllText(invalidFilterPath, "{ \"invalid\": true }");

        // Act
        var projects = MSBuildSolutionLoader.GetProjectFiles(invalidFilterPath);

        // Assert
        Assert.That(projects, Is.Empty);
    }

    [Test]
    public void GetProjectsSkipsSolutionFoldersTest() {
        // Arrange
        var project1 = CreateProject("Project1", SingleTFM, "Library");
        var project2 = CreateProject("Project2", SingleTFM, "Library");
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

        // Act
        var projects = MSBuildSolutionLoader.GetProjectFiles(solutionPath);

        // Assert
        Assert.That(projects, Is.Not.Null);
        Assert.That(projects, Has.Count.EqualTo(2), "Solution folders should be skipped");
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project1)));
        Assert.That(projects.Select(Path.GetFullPath), Contains.Item(Path.GetFullPath(project2)));
    }
}