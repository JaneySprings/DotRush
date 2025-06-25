namespace DotRush.Debugging.NetCore.Tests;

public class DiscoverMSTests : DiscoverTestsBase {
    protected override string TestFixtureAttr => "TestClass";
    protected override string TestCaseAttr => "TestMethod";
    protected override string TestDataAttr => "DataRow";
}