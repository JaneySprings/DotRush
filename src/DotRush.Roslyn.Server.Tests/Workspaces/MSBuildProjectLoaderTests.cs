using DotRush.Common.MSBuild;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class MSBuildProjectLoaderTests : SimpleWorkspaceFixture {
    [TestCase("net10.0")]
    [TestCase("net10.0;net8.0")]
    public void AnalyzeSimpleProject(string frameworkProperty) {
        var projectPath = CreateProject("TestProject1", frameworkProperty);
        var project = MSBuildProjectsLoader.LoadProject(projectPath, resolveAdditionalProperties: true);
        var frameworks = frameworkProperty.Split(';');

        Assert.That(project, Is.Not.Null);
        Assert.That(project.Name, Is.EqualTo("TestProject1"));
        Assert.That(project.FilePath, Is.EqualTo(projectPath));
        Assert.That(project.DirectoryPath, Is.EqualTo(Path.GetDirectoryName(projectPath)));
        Assert.That(project.GetAssemblyName(), Is.EqualTo("TestProject1"));

        Assert.That(project.IsLegacyFormat, Is.False);
        Assert.That(project.IsTestProject, Is.False);

        Assert.That(project.Configurations.Count(), Is.EqualTo(2));
        Assert.That(project.Configurations, Does.Contain("Debug"));
        Assert.That(project.Configurations, Does.Contain("Release"));
        Assert.That(project.Frameworks.Count(), Is.EqualTo(frameworks.Length));
        foreach (var tfm in frameworks)
            Assert.That(project.Frameworks, Does.Contain(tfm));
    }
}