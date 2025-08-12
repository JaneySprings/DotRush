using DotRush.Roslyn.Workspaces.Extensions;
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
}