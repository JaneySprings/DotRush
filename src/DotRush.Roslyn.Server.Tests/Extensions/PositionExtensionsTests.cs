using DotRush.Protocol.Models;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Extensions;

[TestFixture]
public class PositionExtensionsTests {
    private static readonly SourceText sourceText = SourceText.From("class A {\n    int b;\n}");

    [TestCase(0, 0, 0)]
    [TestCase(1, 4, 14)]
    [TestCase(2, 1, 22)]
    public void ToOffsetTest(int line, int character, int expectedOffset) {
        Assert.That(new Position(line, character).ToOffset(sourceText), Is.EqualTo(expectedOffset));
    }

    [TestCase(0, 100, 9)]
    [TestCase(1, 100, 20)]
    [TestCase(3, 0, 22)]
    [TestCase(100, 100, 22)]
    public void ToOffsetOutsideOfDocumentTest(int line, int character, int expectedOffset) {
        Assert.That(new Position(line, character).ToOffset(sourceText), Is.EqualTo(expectedOffset));
    }
}
