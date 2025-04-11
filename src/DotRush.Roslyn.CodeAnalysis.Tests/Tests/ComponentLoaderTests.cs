using DotRush.Roslyn.CodeAnalysis.Components;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace DotRush.Roslyn.CodeAnalysis.Tests;

public class CodeRefactoringProvidersLoaderTests : ComponentsLoaderTests<CodeRefactoringProvider> {
    private CodeRefactoringProvidersLoader loader = null!;
    protected override IComponentLoader<CodeRefactoringProvider> ComponentsLoader => loader;
    protected override int EmbeddedComponentsCount => 78;
    protected override int ProjectComponentsCount => 0;

    [SetUp]
    public void SetUp() {
        loader = new CodeRefactoringProvidersLoader();
    }
}
public class CodeFixProvidersLoaderTests : ComponentsLoaderTests<CodeFixProvider> {
    private CodeFixProvidersLoader loader = null!;
    protected override IComponentLoader<CodeFixProvider> ComponentsLoader => loader;
    protected override int EmbeddedComponentsCount => 163;
    protected override int ProjectComponentsCount => 133;

    [SetUp]
    public void SetUp() {
        loader = new CodeFixProvidersLoader();
    }
}
public class DiagnosticAnalyzersLoaderTests : ComponentsLoaderTests<DiagnosticAnalyzer> {
    private DiagnosticAnalyzersLoader loader = null!;
    protected override IComponentLoader<DiagnosticAnalyzer> ComponentsLoader => loader;
    protected override int EmbeddedComponentsCount => 113;
    protected override int ProjectComponentsCount => 277;

    [SetUp]
    public void SetUp() {
        loader = new DiagnosticAnalyzersLoader();
    }
}


public abstract class ComponentsLoaderTests<TValue> : WorkspaceTestFixture where TValue: class {
    protected abstract IComponentLoader<TValue> ComponentsLoader { get; }
    protected abstract int EmbeddedComponentsCount { get; }
    protected abstract int ProjectComponentsCount { get; }

    protected override string CreateProject(string name, string tfm, string outputType) {
        return base.CreateProject("TestProjectCL", MultiTFM, "Library");
    }

    [Test]
    public virtual void LoadEmbeddedComponentsTest() {
        var components = ComponentsLoader.GetComponents(null);
        Assert.That(components, Has.Length.EqualTo(EmbeddedComponentsCount));
        Assert.That(ComponentsLoader.ComponentsCache.Keys.Count(), Is.EqualTo(3));
        Assert.That(ComponentsLoader.ComponentsCache.Keys, Does.Contain(KnownAssemblies.CommonFeaturesAssemblyName));
        Assert.That(ComponentsLoader.ComponentsCache.Keys, Does.Contain(KnownAssemblies.CSharpFeaturesAssemblyName));
        Assert.That(ComponentsLoader.ComponentsCache.Keys, Does.Contain(KnownAssemblies.DotRushCodeAnalysis));
        Assert.That(ComponentsLoader.ComponentsCache.Count, Is.EqualTo(EmbeddedComponentsCount));

        ComponentsLoader.ComponentsCache.ThrowOnCreation = true;

        components = ComponentsLoader.GetComponents(null);
        Assert.That(components, Has.Length.EqualTo(EmbeddedComponentsCount));
        Assert.That(ComponentsLoader.ComponentsCache.Count, Is.EqualTo(EmbeddedComponentsCount));
    }
    [Test]
    public virtual void LoadProjectComponentsTest() {
        var projects = Workspace!.Solution!.Projects;
        Assert.That(projects.Count(), Is.EqualTo(2));

        foreach (var project in projects) {
            ComponentsLoader.ComponentsCache.ThrowOnCreation = false;
            var components = ComponentsLoader.GetComponents(project);
            Assert.That(components, Has.Length.EqualTo(ProjectComponentsCount + EmbeddedComponentsCount));
            Assert.That(ComponentsLoader.ComponentsCache.Keys, Does.Contain(project.Name));
            Assert.That(ComponentsLoader.ComponentsCache.Count, Is.EqualTo(ProjectComponentsCount + EmbeddedComponentsCount));

            var oldComponentsKeysCount = ComponentsLoader.ComponentsCache.Keys.Count();

            ComponentsLoader.ComponentsCache.ThrowOnCreation = true;
            components = ComponentsLoader.GetComponents(project);
            Assert.That(components, Has.Length.EqualTo(ProjectComponentsCount + EmbeddedComponentsCount));
            Assert.That(ComponentsLoader.ComponentsCache.Count, Is.EqualTo(ProjectComponentsCount + EmbeddedComponentsCount));
            Assert.That(ComponentsLoader.ComponentsCache.Keys.Count(), Is.EqualTo(oldComponentsKeysCount));
        }
    }
}