namespace DotRush.Debugging.NetCore.Tests;

public class DiscoverNUnitTests : DiscoverTestsBase {
    protected override string TestFixtureAttr => "TestFixture";
    protected override string TestCaseAttr => "Test";
    protected override string TestDataAttr => "TestCase";
}