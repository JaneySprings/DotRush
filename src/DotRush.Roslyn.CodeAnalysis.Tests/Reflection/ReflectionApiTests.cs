using System.Reflection;
using System.Text.Json;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.CodeAnalysis.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Tags;
using NUnit.Framework;

namespace DotRush.Roslyn.CodeAnalysis.Tests;

public class ReflectionApiTests : TestFixture {

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
    public void OrganizeImportsOptionsTest() {
        Assert.That(InternalOrganizeImportsOptions.organizeImportsOptionsType, Is.Not.Null);

        Assert.That(InternalOrganizeImportsOptions.placeSystemNamespaceFirstProperty, Is.Not.Null);
        Assert.That(InternalOrganizeImportsOptions.placeSystemNamespaceFirstProperty!.PropertyType, Is.EqualTo(typeof(bool)));

        Assert.That(InternalOrganizeImportsOptions.separateImportDirectiveGroupsProperty, Is.Not.Null);
        Assert.That(InternalOrganizeImportsOptions.separateImportDirectiveGroupsProperty!.PropertyType, Is.EqualTo(typeof(bool)));

        var instance = InternalOrganizeImportsOptions.CreateNew();
        Assert.That(instance, Is.Not.Null);
        Assert.DoesNotThrow(() => InternalOrganizeImportsOptions.AssignValues(instance!, true, false));
    }
    [Test]
    public void CSharpOrganizeImportsServiceTest() {
        Assert.That(InternalCSharpOrganizeImportsService.csharpOrganizeImportsServiceType, Is.Not.Null);
        Assert.That(InternalCSharpOrganizeImportsService.organizeImportsAsyncMethod, Is.Not.Null);

        var parameters = InternalCSharpOrganizeImportsService.organizeImportsAsyncMethod!.GetParameters();
        Assert.That(parameters, Has.Length.EqualTo(3));
        Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(Document)));
        Assert.That(parameters[1].ParameterType.Name, Is.EqualTo("OrganizeImportsOptions"));
        Assert.That(parameters[2].ParameterType, Is.EqualTo(typeof(CancellationToken)));

        var returnType = InternalCSharpOrganizeImportsService.organizeImportsAsyncMethod.ReturnType;
        Assert.That(returnType.IsGenericType, Is.True);
        Assert.That(returnType.GetGenericTypeDefinition(), Is.EqualTo(typeof(Task<>)));
        Assert.That(returnType.GetGenericArguments()[0].Name, Is.EqualTo("Document"));
    }

    [Test]
    public void CompletionOptionsTest() {
        Assert.That(InternalCompletionOptions.completionOptionsType, Is.Not.Null);

        Assert.That(InternalCompletionOptions.showItemsFromUnimportedNamespacesProperty, Is.Not.Null);
        Assert.That(InternalCompletionOptions.showItemsFromUnimportedNamespacesProperty!.PropertyType, Is.EqualTo(typeof(bool?)));

        Assert.That(InternalCompletionOptions.targetTypedCompletionFilterProperty, Is.Not.Null);
        Assert.That(InternalCompletionOptions.targetTypedCompletionFilterProperty!.PropertyType, Is.EqualTo(typeof(bool)));

        var instance = InternalCompletionOptions.CreateNew();
        Assert.That(instance, Is.Not.Null);
        Assert.DoesNotThrow(() => InternalCompletionOptions.AssignValues(instance!, true, false));
    }
    [Test]
    public void GetCompletionsInternalMethodTest() {
        Assert.That(InternalCompletionService.IsInitialized, Is.True, "InternalCompletionService is not initialized.");
        Assert.That(InternalCompletionService.getCompletionsAsyncMethod, Is.Not.Null);

        var parameters = InternalCompletionService.getCompletionsAsyncMethod!.GetParameters();
        Assert.That(parameters, Has.Length.EqualTo(7));
        Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(Document)));
        Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(int)));
        Assert.That(parameters[2].Name, Is.EqualTo("options"));
        Assert.That(parameters[3].ParameterType, Is.EqualTo(typeof(OptionSet)));
        Assert.That(parameters[4].ParameterType, Is.EqualTo(typeof(CompletionTrigger)));
        Assert.That(parameters[5].Name, Is.EqualTo("roles"));
        Assert.That(parameters[6].ParameterType, Is.EqualTo(typeof(CancellationToken)));
    }

    [Test]
    public void WellKnownTagsTest() {
        var wellKnownTagsType = typeof(WellKnownTags);
        Assert.That(wellKnownTagsType, Is.Not.Null);

        var deprecatedField = wellKnownTagsType.GetField("Deprecated", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(deprecatedField, Is.Not.Null);
        Assert.That(deprecatedField!.GetValue(null), Is.EqualTo(InternalWellKnownTags.Deprecated));

        var targetTypeMatchField = wellKnownTagsType.GetField("TargetTypeMatch", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetTypeMatchField, Is.Not.Null);
        Assert.That(targetTypeMatchField!.GetValue(null), Is.EqualTo(InternalWellKnownTags.TargetTypeMatch));
    }
}