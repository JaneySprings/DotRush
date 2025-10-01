using System.Collections.Immutable;
using System.Reflection;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.CodeAnalysis.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

[TestFixture]
public class ReflectionApiTests {

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

    [Test]
    public void BlockStructureOptionsTest() {
        Assert.That(InternalBlockStructureOptions.blockStructureOptionsType, Is.Not.Null);
        var instance = InternalBlockStructureOptions.CreateNew();
        Assert.That(instance, Is.Not.Null);
    }
    [Test]
    public void CSharpBlockStructureServiceTest() {
        Assert.That(InternalCSharpBlockStructureService.csharpBlockStructureServiceType, Is.Not.Null);
        Assert.That(InternalCSharpBlockStructureService.getBlockStructureAsyncMethod, Is.Not.Null);

        var parameters = InternalCSharpBlockStructureService.getBlockStructureAsyncMethod!.GetParameters();
        Assert.That(parameters, Has.Length.EqualTo(3));
        Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(Document)));
        Assert.That(parameters[1].ParameterType.Name, Is.EqualTo("BlockStructureOptions"));
        Assert.That(parameters[2].ParameterType, Is.EqualTo(typeof(CancellationToken)));

        var returnType = InternalCSharpBlockStructureService.getBlockStructureAsyncMethod.ReturnType;
        Assert.That(returnType.IsGenericType, Is.True);
        Assert.That(returnType.GetGenericTypeDefinition(), Is.EqualTo(typeof(Task<>)));
        Assert.That(returnType.GetGenericArguments()[0].Name, Is.EqualTo("BlockStructure"));
    }
    [Test]
    public void BlockStructureTest() {
        Assert.That(InternalBlockStructure.blockStructureType, Is.Not.Null);
        Assert.That(InternalBlockStructure.blockSpanType, Is.Not.Null);
        Assert.That(InternalBlockStructure.spansProperty, Is.Not.Null);
        Assert.That(InternalBlockStructure.bannerTextProperty, Is.Not.Null);
        Assert.That(InternalBlockStructure.textSpanProperty, Is.Not.Null);
    }

    [Test]
    public void InlineHintsOptionsTest() {
        Assert.That(InternalInlineHintsOptions.inlineHintsOptionsType, Is.Not.Null);
        Assert.That(InternalInlineHintsOptions.Default, Is.Not.Null);

        var instance = InternalInlineHintsOptions.CreateNew();
        Assert.That(instance, Is.Not.Null);
    }
    [Test]
    public void CSharpInlineHintsServiceTest() {
        Assert.That(InternalCSharpInlineHintsService.csharpCSharpInlineHintsServiceType, Is.Not.Null);
        Assert.That(InternalCSharpInlineHintsService.getInlineHintsAsyncMethod, Is.Not.Null);

        var parameters = InternalCSharpInlineHintsService.getInlineHintsAsyncMethod!.GetParameters();
        Assert.That(parameters, Has.Length.EqualTo(5));
        Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(Document)));
        Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(TextSpan)));
        Assert.That(parameters[2].ParameterType.Name, Is.EqualTo("InlineHintsOptions"));
        Assert.That(parameters[3].ParameterType, Is.EqualTo(typeof(bool)));
        Assert.That(parameters[4].ParameterType, Is.EqualTo(typeof(CancellationToken)));

        var returnType = InternalCSharpInlineHintsService.getInlineHintsAsyncMethod.ReturnType;
        Assert.That(returnType.IsGenericType, Is.True);
        Assert.That(returnType.GetGenericTypeDefinition(), Is.EqualTo(typeof(Task<>)));

        var instance = InternalCSharpInlineHintsService.CreateNew();
        Assert.That(instance, Is.Not.Null);
    }
    [Test]
    public void InlineHintTest() {
        Assert.That(InternalInlineHint.inlineHintType, Is.Not.Null);
        Assert.That(InternalInlineHint.spanField, Is.Not.Null);
        Assert.That(InternalInlineHint.replacementTextChangeField, Is.Not.Null);
        Assert.That(InternalInlineHint.displatPartsField, Is.Not.Null);

        Assert.That(InternalInlineHint.spanField!.FieldType, Is.EqualTo(typeof(TextSpan)));
        Assert.That(InternalInlineHint.replacementTextChangeField!.FieldType, Is.EqualTo(typeof(TextChange?)));
        Assert.That(InternalInlineHint.displatPartsField!.FieldType, Is.EqualTo(typeof(ImmutableArray<TaggedText>)));
    }

    [Test]
    public void CompletionItemProviderNameTest() {
        var item = CompletionItem.Create("testItem");
        var providerName = InternalCompletionItem.GetProviderName(item);
        Assert.That(providerName, Is.Null);
    }
}