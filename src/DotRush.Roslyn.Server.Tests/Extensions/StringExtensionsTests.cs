using NUnit.Framework;
using DotRush.Common.Extensions;

namespace DotRush.Roslyn.Server.Extensions;

[TestFixture]
public class StringExtensionsTests {

    [TestCase("Hello", true)]
    [TestCase("hello", false)]
    [TestCase("H", true)]
    [TestCase("h", false)]
    [TestCase("123Hello", false)]
    [TestCase("", false)]
    public void StartsWithUpperTests(string value, bool expected) {
        Assert.That(value.StartsWithUpper(), Is.EqualTo(expected));
    }

    [TestCase("HelloWorld", "helloWorld")]
    [TestCase("HELLO", "hELLO")]
    [TestCase("hello", "hello")]
    [TestCase("H", "h")]
    [TestCase("h", "h")]
    [TestCase("", "")]
    public void ToCamelCaseTests(string value, string expected) {
        Assert.That(value.ToCamelCase(), Is.EqualTo(expected));
    }

    [Test]
    public void SplitByCamelCaseWithSimpleCamelCase() {
        var result = "helloWorld".SplitByCase();
        var expected = new[] { "hello", "World" };
        Assert.That(result, Is.EqualTo(expected));
    }
    [Test]
    public void SplitByCamelCaseWithMultipleWords() {
        var result = "thisIsATestString".SplitByCase();
        var expected = new[] { "this", "Is", "ATest", "String" };
        Assert.That(result, Is.EqualTo(expected));
    }
    [Test]
    public void SplitByCamelCaseWithConsecutiveUppercase() {
        var result = "HTTPSConnection".SplitByCase();
        var expected = new[] { "HTTPSConnection" };
        Assert.That(result, Is.EqualTo(expected));
    }
    [Test]
    public void SplitByCamelCaseWithUppercaseFollowedByLowercase() {
        var result = "XMLHttpRequest".SplitByCase();
        var expected = new[] { "XMLHttp", "Request" };
        Assert.That(result, Is.EqualTo(expected));
    }
    [Test]
    public void SplitByCamelCaseWithSingleWord() {
        var result = "hello".SplitByCase();
        var expected = new[] { "hello" };
        Assert.That(result, Is.EqualTo(expected));
    }
    [Test]
    public void SplitByCamelCaseWithSingleUppercaseWord() {
        var result = "HELLO".SplitByCase();
        var expected = new[] { "HELLO" };
        Assert.That(result, Is.EqualTo(expected));
    }
    [Test]
    public void SplitByCamelCaseWithEmptyString() {
        var result = "".SplitByCase();
        Assert.That(result, Is.EqualTo(Array.Empty<string>()));
    }
    [Test]
    public void SplitByCamelCaseWithSingleCharacter() {
        var result = "A".SplitByCase();
        var expected = new[] { "A" };
        Assert.That(result, Is.EqualTo(expected));
    }
    [Test]
    public void SplitByCamelCaseWithNumbersInString() {
        var result = "version2Update".SplitByCase();
        var expected = new[] { "version2", "Update" };
        Assert.That(result, Is.EqualTo(expected));
    }
}