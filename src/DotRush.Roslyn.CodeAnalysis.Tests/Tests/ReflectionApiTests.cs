using System.Reflection;
using Microsoft.CodeAnalysis.Completion;
using NUnit.Framework;

namespace DotRush.Roslyn.CodeAnalysis.Tests;

public class ReflectionApiTests : TestFixture {

    [Test]
    public void CompletionOptionsTest() {
        var completionOptionsType = typeof(CompletionService).Assembly.GetType("Microsoft.CodeAnalysis.Completion.CompletionOptions");
        Assert.That(completionOptionsType, Is.Not.Null);

        var completionOptions = Activator.CreateInstance(completionOptionsType!);
        var sifunProperty = completionOptionsType!.GetProperty("ShowItemsFromUnimportedNamespaces");
        Assert.That(sifunProperty, Is.Not.Null);
        sifunProperty!.SetValue(completionOptions, false);
    }

    [Test]
    public void GetCompletionsInternalMethodTest() {
        var getCompletionsAsyncMethod = typeof(CompletionService).GetMethod("GetCompletionsAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(getCompletionsAsyncMethod, Is.Not.Null);
        
        var parameters = getCompletionsAsyncMethod!.GetParameters();
        Assert.That(parameters, Has.Length.EqualTo(7));
        Assert.That(parameters[0].Name, Is.EqualTo("document"));
        Assert.That(parameters[1].Name, Is.EqualTo("caretPosition"));
        Assert.That(parameters[2].Name, Is.EqualTo("options"));
        Assert.That(parameters[3].Name, Is.EqualTo("passThroughOptions"));
        Assert.That(parameters[4].Name, Is.EqualTo("trigger"));
        Assert.That(parameters[5].Name, Is.EqualTo("roles"));
        Assert.That(parameters[6].Name, Is.EqualTo("cancellationToken"));
    }
}