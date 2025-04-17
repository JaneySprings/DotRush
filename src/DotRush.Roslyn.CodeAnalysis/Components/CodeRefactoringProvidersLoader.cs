using System.Collections.Immutable;
using System.Reflection;
using DotRush.Common.Extensions;
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
            return dotrushComponents.AddRanges(roslynCoreComponents, roslynCSharpComponents).ToImmutableArray();

        var projectProviders = ComponentsCache.GetOrCreate(project.Name, () => LoadFromProject(project));
        return dotrushComponents.AddRanges(roslynCoreComponents, roslynCSharpComponents, projectProviders).ToImmutableArray();
    }
    
    public List<CodeRefactoringProvider> LoadFromAssembly(string assemblyName) {
        var result = new List<CodeRefactoringProvider>();
        var assemblyTypes = ReflectionExtensions.LoadAssembly(assemblyName);
        if (assemblyTypes == null)
            return result;

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
        return result;
    }
    public List<CodeRefactoringProvider> LoadFromProject(Project project) {
        var analyzerReferenceAssemblies = project.AnalyzerReferences.Select(it => it.FullPath);
        var result = analyzerReferenceAssemblies.SelectMany(it => LoadFromAssembly(it ?? string.Empty)).ToList();
        currentClassLogger.Debug($"Loaded {result.Count} codeRefactoringProviders from project '{project.Name}'");
        return result;
    }
    public List<CodeRefactoringProvider> LoadFromDotRush() {
        return new List<CodeRefactoringProvider> {
            new OrganizeImportsRefactoringProvider(),
        };
    }
}