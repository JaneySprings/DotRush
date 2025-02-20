using Xunit;
using System.Reflection;
using DotRush.Roslyn.Common.Extensions;

namespace DotRush.Roslyn.Tests;

[Collection("Sequential")]
public abstract class TestFixtureBase {
    protected TestFixtureBase() {
        SafeExtensions.ThrowOnExceptions = true;
    }
}