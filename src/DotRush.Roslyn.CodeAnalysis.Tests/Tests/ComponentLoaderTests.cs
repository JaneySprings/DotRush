using DotRush.Roslyn.CodeAnalysis.Components;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace DotRush.Roslyn.CodeAnalysis.Tests;

public class CodeRefactoringProvidersLoaderTests : ComponentsLoaderTests<CodeRefactoringProvider> {
    private CodeRefactoringProvidersLoader loader = null!;
    protected override IComponentLoader<CodeRefactoringProvider> ComponentsLoader => loader;

    [SetUp]
    public void SetUp() {
        loader = new CodeRefactoringProvidersLoader();
    }
}
public class CodeFixProvidersLoaderTests : ComponentsLoaderTests<CodeFixProvider> {
    private CodeFixProvidersLoader loader = null!;
    protected override IComponentLoader<CodeFixProvider> ComponentsLoader => loader;

    [SetUp]
    public void SetUp() {
        loader = new CodeFixProvidersLoader();
    }
}
public class DiagnosticAnalyzersLoaderTests : ComponentsLoaderTests<DiagnosticAnalyzer> {
    private DiagnosticAnalyzersLoader loader = null!;
    protected override IComponentLoader<DiagnosticAnalyzer> ComponentsLoader => loader;

    [SetUp]
    public void SetUp() {
        loader = new DiagnosticAnalyzersLoader();
    }

    [Test, Ignore("Not loaded any embedded analyzers yet")]
    public override void LoadEmbeddedComponentsTest() {}
}


public abstract class ComponentsLoaderTests<TValue> : WorkspaceTestFixture where TValue: class {
    protected const string MultiTFM = "net8.0;net9.0";

    protected abstract IComponentLoader<TValue> ComponentsLoader { get; }

    protected override string CreateProject(string name, string tfm, string outputType) {
        return base.CreateProject("TestProjectCL", MultiTFM, "Library");
    }

    [Test]
    public virtual void LoadEmbeddedComponentsTest() {
        var components = ComponentsLoader.GetComponents(null);
        Assert.That(components, Is.Not.Empty);
        Assert.That(ComponentsLoader.ComponentsCache.Keys.Count(), Is.EqualTo(3));
        Assert.That(ComponentsLoader.ComponentsCache.Keys, Does.Contain(KnownAssemblies.CommonFeaturesAssemblyName));
        Assert.That(ComponentsLoader.ComponentsCache.Keys, Does.Contain(KnownAssemblies.CSharpFeaturesAssemblyName));
        Assert.That(ComponentsLoader.ComponentsCache.Keys, Does.Contain(KnownAssemblies.DotRushCodeAnalysis));
        Assert.That(ComponentsLoader.ComponentsCache.Count, Is.Not.Zero);

        var oldComponentsCount = components.Length;
        var oldComponentsCacheCount = ComponentsLoader.ComponentsCache.Count;
        ComponentsLoader.ComponentsCache.ThrowOnCreation = true;

        components = ComponentsLoader.GetComponents(null);
        Assert.That(components, Has.Length.EqualTo(oldComponentsCount));
        Assert.That(ComponentsLoader.ComponentsCache.Count, Is.EqualTo(oldComponentsCacheCount));
    }
    [Test]
    public virtual void LoadProjectComponentsTest() {
        var projects = Workspace!.Solution!.Projects;
        Assert.That(projects.Count(), Is.EqualTo(2));

        var globalComponentsCacheCount = 0;
        foreach (var project in projects) {
            ComponentsLoader.ComponentsCache.ThrowOnCreation = false;
            var components = ComponentsLoader.GetComponents(project);
            Assert.That(components, Is.Not.Empty);
            Assert.That(ComponentsLoader.ComponentsCache.Keys, Does.Contain(project.Name));

            var oldComponentsCacheCount = ComponentsLoader.ComponentsCache.Count;
            var oldComponentsKeysCount = ComponentsLoader.ComponentsCache.Keys.Count();

            ComponentsLoader.ComponentsCache.ThrowOnCreation = true;
            components = ComponentsLoader.GetComponents(project);
            Assert.That(ComponentsLoader.ComponentsCache.Count, Is.EqualTo(oldComponentsCacheCount));
            Assert.That(ComponentsLoader.ComponentsCache.Keys.Count(), Is.EqualTo(oldComponentsKeysCount));

            if (globalComponentsCacheCount != 0)
                Assert.That(ComponentsLoader.ComponentsCache.Count, Is.EqualTo(globalComponentsCacheCount));
            globalComponentsCacheCount = ComponentsLoader.ComponentsCache.Count;
        }
    }
}