using NUnit.Framework;

namespace DotRush.Roslyn.CodeAnalysis.Tests;

[TestFixture]
public abstract class TestFixture {
    protected const string MultiTFM = "net8.0;net9.0";
}