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
}