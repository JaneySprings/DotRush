using System.Reflection;
using DotRush.Roslyn.CodeAnalysis.Extensions;
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

    [TestCase("example.dll", "example")]
    [TestCase("path/to/example.dll", "example")]
    [TestCase("example", "example")]
    [TestCase("path/to/example", "example")]
    [TestCase("Microsoft.CodeAnalysis.Features", "Microsoft.CodeAnalysis.Features")]
    [TestCase("Microsoft.CodeAnalysis.dll", "Microsoft.CodeAnalysis")]
    public void GetAssemblyNameTest(string input, string expected) {
        var result = ReflectionExtensions.GetAssemblyName(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void OrganizeImportsOptionsTypeTest() {
        var organizeImportsOptionsType = ReflectionExtensions.GetTypeFromLoadedAssembly(
            KnownAssemblies.WorkspacesAssemblyName, 
            "Microsoft.CodeAnalysis.OrganizeImports.OrganizeImportsOptions");
        
        Assert.That(organizeImportsOptionsType, Is.Not.Null);
        
        var placeSystemFirstProperty = organizeImportsOptionsType!.GetProperty("PlaceSystemNamespaceFirst");
        Assert.That(placeSystemFirstProperty, Is.Not.Null);
        Assert.That(placeSystemFirstProperty!.PropertyType, Is.EqualTo(typeof(bool)));

        var separateGroupsProperty = organizeImportsOptionsType.GetProperty("SeparateImportDirectiveGroups");
        Assert.That(separateGroupsProperty, Is.Not.Null);
        Assert.That(separateGroupsProperty!.PropertyType, Is.EqualTo(typeof(bool)));
    }
    [Test]
    public void CSharpOrganizeImportsServiceTest() {
        var organizeImportsServiceType = ReflectionExtensions.GetTypeFromLoadedAssembly(
            KnownAssemblies.CSharpWorkspacesAssemblyName, 
            "Microsoft.CodeAnalysis.CSharp.OrganizeImports.CSharpOrganizeImportsService");
        
        Assert.That(organizeImportsServiceType, Is.Not.Null);

        var organizeImportsAsyncMethod = organizeImportsServiceType!.GetMethod("OrganizeImportsAsync");
        Assert.That(organizeImportsAsyncMethod, Is.Not.Null);
        
        var parameters = organizeImportsAsyncMethod!.GetParameters();
        Assert.That(parameters, Has.Length.EqualTo(3));
        Assert.That(parameters[0].ParameterType.Name, Is.EqualTo("Document"));
        Assert.That(parameters[1].ParameterType.Name, Is.EqualTo("OrganizeImportsOptions"));
        Assert.That(parameters[2].ParameterType, Is.EqualTo(typeof(CancellationToken)));
        
        var returnType = organizeImportsAsyncMethod.ReturnType;
        Assert.That(returnType.IsGenericType, Is.True);
        Assert.That(returnType.GetGenericTypeDefinition(), Is.EqualTo(typeof(Task<>)));
        Assert.That(returnType.GetGenericArguments()[0].Name, Is.EqualTo("Document"));
    }
}