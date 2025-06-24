using DotRush.Common.MSBuild;
using DotRush.Debugging.NetCore.Testing.Models;

namespace DotRush.Debugging.NetCore.Testing.Explorer;

public class TestExplorer : TestExplorerSyntaxWalker {
    public IEnumerable<TestFixture> DiscoverTests(string projectFile) {
        var project = MSBuildProjectsLoader.LoadProject(projectFile);
        if (project == null || !project.IsTestProject())
            return Enumerable.Empty<TestFixture>();

        var testProjectDirectory = Path.GetDirectoryName(projectFile)!;
        return DiscoverTestsCore(testProjectDirectory);
    }

    private List<TestFixture> DiscoverTestsCore(string projectDirectory) {
        var result = new List<TestFixture>();
        var fixtures = GetFixtures(projectDirectory);
        var fixturesDict = fixtures.ToDictionary(f => f.Id);
        
        // Process fixtures that are not nested (no parent fixture)
        foreach (var fixture in fixtures) {
            if (fixture.IsAbstract || !string.IsNullOrEmpty(fixture.ParentFixtureId))
                continue;

            fixture.Resolve(fixtures);
            
            // Only add top-level fixtures with tests or child fixtures
            if (fixture.TestCases.Count != 0 || fixture.ChildFixtures.Count != 0)
                result.Add(fixture);
        }

        return result;
    }
}
