using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Workspaces.Extensions;
using NUnit.Framework;

namespace DotRush.Roslyn.Workspaces.Tests;

public class ManipulationTests : TestFixture {
    public ManipulationTests() {
        SafeExtensions.ThrowOnExceptions = true;
    }

    [Test]
    public async Task IntermediateDirectoriesTest() {
        var workspace = new TestWorkspace();
        var projectPath = CreateProject("MyProject", MultiTFM, "Exe");

        await workspace.LoadAsync(new[] { projectPath }, CancellationToken.None);
        foreach (var project in workspace.Solution!.Projects) {
            var tfm = project.GetTargetFramework();
            Assert.That(project.GetIntermediateOutputPath(), Is.EqualTo(Path.Combine(Path.GetDirectoryName(projectPath)!, "obj", "Debug", tfm)));
            Assert.That(project.GetOutputPath(), Is.EqualTo(Path.Combine(Path.GetDirectoryName(projectPath)!, "bin", "Debug", tfm)));
        }
    }
}