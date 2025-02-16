using NUnit.Framework;

namespace DotRush.Roslyn.CodeAnalysis.Tests;

[TestFixture]
public abstract class TestFixture {
    [SetUp]
    public void Setup() {
    }
    [TearDown]
    public void TearDown() {
    }
}