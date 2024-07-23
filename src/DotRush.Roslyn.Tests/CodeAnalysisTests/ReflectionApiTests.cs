using System.Reflection;
using Microsoft.CodeAnalysis.Completion;
using Xunit;

namespace DotRush.Roslyn.Tests.CodeAnalysisTests;

public class ReflectionApiTests : TestFixtureBase {

    [Fact]
    public void CompletionOptionsTest() {
        var completionOptionsType = typeof(CompletionService).Assembly.GetType("Microsoft.CodeAnalysis.Completion.CompletionOptions");
        Assert.NotNull(completionOptionsType);

        var completionOptions = Activator.CreateInstance(completionOptionsType);
        var sifunProperty = completionOptionsType.GetProperty("ShowItemsFromUnimportedNamespaces");
        Assert.NotNull(sifunProperty);
        sifunProperty.SetValue(completionOptions, false);
    }

    [Fact]
    public void GetCompletionsInternalMethodTest() {
        var getCompletionsAsyncMethod = typeof(CompletionService).GetMethod("GetCompletionsAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(getCompletionsAsyncMethod);
        
        var parameters = getCompletionsAsyncMethod.GetParameters();
        Assert.Equal(7, parameters.Length);
        Assert.Equal("document", parameters[0].Name);
        Assert.Equal("caretPosition", parameters[1].Name);
        Assert.Equal("options", parameters[2].Name);
        Assert.Equal("passThroughOptions", parameters[3].Name);
        Assert.Equal("trigger", parameters[4].Name);
        Assert.Equal("roles", parameters[5].Name);
        Assert.Equal("cancellationToken", parameters[6].Name);
    }
}