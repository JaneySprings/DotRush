using DotRush.Debugging.NetCore.Extensions;
using NUnit.Framework;

namespace DotRush.Debugging.NetCore.Tests;

public class NUnitFilterExtensionsUT
{
    [TestCase("Category=foo", "cat==foo")]
    [TestCase("Category!=bar", "cat!=bar")]
    [TestCase("Category=foo|Category=bar", "(cat==foo or cat==bar)")]
    [TestCase("Category=foo&Category!=bar", "(cat==foo and cat!=bar)")]
    [TestCase("(Category=foo)&(Category!=bar)", "(cat==foo and cat!=bar)")]
    [TestCase("Category=foo|Category!=baz&Category=bar", "(cat==foo or (cat!=baz and cat==bar))")]
    public void TranslateRunSettingsCategoryFilter_Works(string filter, string expected) {
        var result = InvokeTranslate(filter);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void TranslateRunSettingsCategoryFilter_IgnoresNonCategory() {
        var result = InvokeTranslate("FullyQualifiedName=MinimalApiExample.Tests.Test1");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void UpdateRunSettingsWithNUnitFilter_AddsWhereWhenEmptyRunSettings() {
        var runsettings = NUnitFilterExtensions.UpdateRunSettingsWithNUnitFilter(
            null,
            new[] { "Some/NUnit.dll" },
            new string[0]
        );

        Assert.That(runsettings, Does.Contain("<NUnit>"));
        Assert.That(runsettings, Does.Contain("<Where>"));
    }

    private static string? InvokeTranslate(string filter) {
        return NUnitFilterExtensions.TranslateRunSettingsCategoryFilter(filter);
    }
}
