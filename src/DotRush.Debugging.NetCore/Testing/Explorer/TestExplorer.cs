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
        foreach (var fixture in fixtures) {
            if (fixture.IsAbstract)
                continue;

            fixture.Resolve(fixtures);
          
            // Only add top-level fixtures with tests or child fixtures that have tests
            if (HasTestCasesRecursively(fixture))
                result.Add(fixture);
        }

        return result;
    }
    
    /// <summary>
    /// Checks recursively if a fixture or any of its child fixtures contain test cases
    /// </summary>
    /// <param name="fixture">The fixture to check</param>
    /// <returns>True if the fixture or any of its child fixtures contain test cases</returns>
    private bool HasTestCasesRecursively(TestFixture fixture) {
        if (fixture.TestCases.Count > 0)
            return true;
            
        foreach (var childFixture in fixture.ChildFixtures) {
            if (HasTestCasesRecursively(childFixture))
                return true;
        }
        
        return false;
    }
}
