namespace DotRush.Roslyn.Workspaces.Tests;

public class DiscoverXUnitTests : DiscoverTestsBase {
    protected override string TestFixtureAttr => "Collection";
    protected override string TestCaseAttr => "Fact";
    protected override string TestDataAttr => "Theory";
}