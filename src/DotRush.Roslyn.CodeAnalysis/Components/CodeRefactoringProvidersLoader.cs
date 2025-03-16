using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Reflection;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Embedded.Refactorings;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace DotRush.Roslyn.CodeAnalysis.Components;

public class CodeRefactoringProvidersLoader : IComponentLoader<CodeRefactoringProvider> {
    public MemoryCache<CodeRefactoringProvider> ComponentsCache { get; } = new MemoryCache<CodeRefactoringProvider>();
    private readonly CurrentClassLogger currentClassLogger = new CurrentClassLogger(nameof(CodeRefactoringProvidersLoader));

    public ImmutableArray<CodeRefactoringProvider> GetComponents(Project? project = null) {
        var dotrushComponents = ComponentsCache.GetOrCreate(KnownAssemblies.DotRushCodeAnalysis, () => LoadFromDotRush());
        var roslynCoreComponents = ComponentsCache.GetOrCreate(KnownAssemblies.CommonFeaturesAssemblyName, () => LoadFromAssembly(KnownAssemblies.CommonFeaturesAssemblyName));
        var roslynCSharpComponents = ComponentsCache.GetOrCreate(KnownAssemblies.CSharpFeaturesAssemblyName, () => LoadFromAssembly(KnownAssemblies.CSharpFeaturesAssemblyName));
        if (project == null)
            return dotrushComponents.Concat(roslynCoreComponents).Concat(roslynCSharpComponents).ToImmutableArray();

        var projectProviders = ComponentsCache.GetOrCreate(project.Name, () => LoadFromProject(project));
        return dotrushComponents.Concat(roslynCoreComponents).Concat(roslynCSharpComponents).Concat(projectProviders).ToImmutableArray();
    }
    
    public ReadOnlyCollection<CodeRefactoringProvider> LoadFromAssembly(string assemblyName) {
        var result = new List<CodeRefactoringProvider>();
        var assemblyTypes = ReflectionExtensions.LoadAssembly(assemblyName);
        if (assemblyTypes == null)
            return new ReadOnlyCollection<CodeRefactoringProvider>(result);

        var providersInfo = assemblyTypes.Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(CodeRefactoringProvider)));
        foreach (var providerInfo in providersInfo) {
            try {
                var attribute = providerInfo.GetCustomAttribute<ExportCodeRefactoringProviderAttribute>();
                if (attribute == null) {
                    currentClassLogger.Debug($"Skipping code refactoring provider '{providerInfo.Name}' because it is missing the '${nameof(ExportCodeRefactoringProviderAttribute)}'");
                    continue;
                }
                if (Activator.CreateInstance(providerInfo.AsType()) is not CodeRefactoringProvider instance) {
                    currentClassLogger.Error($"Instance of code refactoring provider '{providerInfo.Name}' is null");
                    continue;
                }
                result.Add(instance);
                currentClassLogger.Debug($"Loaded code refacotring provider: {instance}");
            } catch (Exception ex) {
                currentClassLogger.Error($"Creating instance of '{providerInfo.Name}' failed, error: {ex}");
            }
        }
        currentClassLogger.Debug($"Loaded {result.Count} codeRefactoringProviders form assembly '{assemblyName}'");
        return new ReadOnlyCollection<CodeRefactoringProvider>(result);
    }
    public ReadOnlyCollection<CodeRefactoringProvider> LoadFromProject(Project project) {
        var analyzerReferenceAssemblies = project.AnalyzerReferences.Select(it => it.FullPath);
        var result = analyzerReferenceAssemblies.SelectMany(it => LoadFromAssembly(it ?? string.Empty)).ToArray();
        currentClassLogger.Debug($"Loaded {result.Length} codeRefactoringProviders from project '{project.Name}'");
        return new ReadOnlyCollection<CodeRefactoringProvider>(result);
    }
    public ReadOnlyCollection<CodeRefactoringProvider> LoadFromDotRush() {
        return new ReadOnlyCollection<CodeRefactoringProvider>(new List<CodeRefactoringProvider> {
            new OrganizeImportsRefactoringProvider(),
        });
    }
}