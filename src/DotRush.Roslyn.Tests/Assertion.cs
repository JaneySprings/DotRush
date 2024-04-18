using Xunit;

namespace DotRush.Roslyn.Tests;

public static class Assertion {
    public static void CollectionsAreEqual<TValue>(List<TValue> expected, List<TValue> actual) {
        actual.ForEach(it => Assert.Contains(it, expected));
        expected.ForEach(it => Assert.Contains(it, actual));
    }
}