using DotRush.Roslyn.CodeAnalysis.Components;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace DotRush.Roslyn.CodeAnalysis.Tests;

public class CodeRefactoringProvidersLoaderTests : ComponentsLoaderTests<CodeRefactoringProvider> {
    private CodeRefactoringProvidersLoader loader = null!;
    protected override IComponentLoader<CodeRefactoringProvider> ComponentsLoader => loader;
    protected override int ComponentsCount => 80;

    [SetUp]
    public void SetUp() {
        loader = new CodeRefactoringProvidersLoader(this);
    }
}
public class CodeFixProvidersLoaderTests : ComponentsLoaderTests<CodeFixProvider> {
    private CodeFixProvidersLoader loader = null!;
    protected override IComponentLoader<CodeFixProvider> ComponentsLoader => loader;
    protected override int ComponentsCount => 298;

    [SetUp]
    public void SetUp() {
        loader = new CodeFixProvidersLoader(this);
    }
}
public class DiagnosticAnalyzersLoaderTests : ComponentsLoaderTests<DiagnosticAnalyzer> {
    private DiagnosticAnalyzersLoader loader = null!;
    protected override IComponentLoader<DiagnosticAnalyzer> ComponentsLoader => loader;
    protected override int ComponentsCount => 393;
    protected int SuppressorsCount => 1;

    [SetUp]
    public void SetUp() {
        loader = new DiagnosticAnalyzersLoader(this);
    }

    [Test]
    public void LoadProjectSuppressorsTest() {
        var projects = Workspace!.Solution!.Projects;
        Assert.That(projects.Count(), Is.EqualTo(2));

        foreach (var project in projects) {
            ComponentsLoader.ComponentsCache.ThrowOnCreation = false;
            var components = loader.GetSuppressors(project);
            Assert.That(components, Has.Length.EqualTo(SuppressorsCount));
            Assert.That(ComponentsLoader.ComponentsCache.Keys, Does.Contain(project.Name));
            Assert.That(ComponentsLoader.ComponentsCache.Count, Is.EqualTo(ComponentsCount));

            var oldComponentsKeysCount = ComponentsLoader.ComponentsCache.Keys.Count();

            ComponentsLoader.ComponentsCache.ThrowOnCreation = true;
            components = loader.GetSuppressors(project);
            Assert.That(components, Has.Length.EqualTo(SuppressorsCount));
            Assert.That(ComponentsLoader.ComponentsCache.Count, Is.EqualTo(ComponentsCount));
            Assert.That(ComponentsLoader.ComponentsCache.Keys.Count(), Is.EqualTo(oldComponentsKeysCount));
        }
    }
}


public abstract class ComponentsLoaderTests<TValue> : WorkspaceTestFixture, IAdditionalComponentsProvider where TValue : class {
    protected abstract IComponentLoader<TValue> ComponentsLoader { get; }
    protected abstract int ComponentsCount { get; }

    protected override string CreateProject(string name, string tfm, string outputType) {
        return base.CreateProject("TestProjectCL", MultiTFM, "Library");
    }

    [Test]
    public virtual void LoadProjectComponentsTest() {
        var projects = Workspace!.Solution!.Projects;
        Assert.That(projects.Count(), Is.EqualTo(2));

        foreach (var project in projects) {
            ComponentsLoader.ComponentsCache.ThrowOnCreation = false;
            var components = ComponentsLoader.GetComponents(project);
            Assert.That(components, Has.Length.EqualTo(ComponentsCount));
            Assert.That(ComponentsLoader.ComponentsCache.Keys, Does.Contain(project.Name));
            Assert.That(ComponentsLoader.ComponentsCache.Count, Is.EqualTo(ComponentsCount));

            var oldComponentsKeysCount = ComponentsLoader.ComponentsCache.Keys.Count();

            ComponentsLoader.ComponentsCache.ThrowOnCreation = true;
            components = ComponentsLoader.GetComponents(project);
            Assert.That(components, Has.Length.EqualTo(ComponentsCount));
            Assert.That(ComponentsLoader.ComponentsCache.Count, Is.EqualTo(ComponentsCount));
            Assert.That(ComponentsLoader.ComponentsCache.Keys.Count(), Is.EqualTo(oldComponentsKeysCount));
        }
    }

    bool IAdditionalComponentsProvider.IsEnabled => false;
    IEnumerable<string> IAdditionalComponentsProvider.GetAdditionalAssemblies() {
        throw new NotImplementedException("This method is not implemented in the test project");
    }
}