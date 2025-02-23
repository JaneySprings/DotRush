using DotRush.Common.Logging;
using DotRush.Common.MSBuild;
using DotRush.Debugging.NetCore.Testing.Models;

namespace DotRush.Debugging.NetCore.Testing.Explorer;

public class TestExplorer : TestExplorerSyntaxWalker {
    public IEnumerable<TestFixture> DiscoverTests(string projectFile) {
        var project = MSBuildProjectsLoader.LoadProject(projectFile);
        if (project != null && project.IsLegacyFormat) {
            CurrentSessionLogger.Debug($"Project {projectFile} is in legacy format. Skipping test discovery.");
            return Enumerable.Empty<TestFixture>();
        }
        if (project != null && !project.HasPackage("NUnit") && !project.HasPackage("NUnitLite") && !project.HasPackage("xunit")) {
            CurrentSessionLogger.Debug($"Project {projectFile} does not have NUnit or xUnit package. Skipping test discovery.");
            return Enumerable.Empty<TestFixture>();
        }

        var testProjectDirectory = Path.GetDirectoryName(projectFile)!;
        return DiscoverTestsCore(testProjectDirectory);
    }

    private List<TestFixture> DiscoverTestsCore(string projectDirectory) {
        var result = new List<TestFixture>();
        var fixtures = GetFixtures(projectDirectory);
        foreach (var fixture in fixtures) {
            if (fixture.IsAbstract)
                continue;

            fixture.Resolve(fixtures);
            if (fixture.TestCases.Count != 0)
                result.Add(fixture);
        }

        return result;
    }
}
