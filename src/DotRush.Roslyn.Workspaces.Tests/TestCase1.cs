using NUnit.Framework;

namespace DotRush.Roslyn.Workspaces.Tests;

[TestFixture]
public class TestCase1 {

    [Test]
    public void TestMethod() {
        var x = 1;
        var y = 2;
        Assert.That(x + y, Is.EqualTo(3));
    }
}