using System.Collections.Immutable;
using System.Reflection;
using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DotRush.Roslyn.CodeAnalysis.Components;

public class CodeFixProvidersLoader : IComponentLoader<CodeFixProvider> {
    public MemoryCache<CodeFixProvider> ComponentsCache { get; } = new MemoryCache<CodeFixProvider>();
    private readonly CurrentClassLogger currentClassLogger = new CurrentClassLogger(nameof(CodeFixProvidersLoader));

    public ImmutableArray<CodeFixProvider> GetComponents(Project? project = null) {
        var dotrushComponents = ComponentsCache.GetOrCreate(KnownAssemblies.DotRushCodeAnalysis, () => LoadFromDotRush());
        var roslynCoreComponents = ComponentsCache.GetOrCreate(KnownAssemblies.CommonFeaturesAssemblyName, () => LoadFromAssembly(KnownAssemblies.CommonFeaturesAssemblyName));
        var roslynCSharpComponents = ComponentsCache.GetOrCreate(KnownAssemblies.CSharpFeaturesAssemblyName, () => LoadFromAssembly(KnownAssemblies.CSharpFeaturesAssemblyName));
        if (project == null)
            return dotrushComponents.AddRanges(roslynCoreComponents, roslynCSharpComponents).ToImmutableArray();

        var projectProviders = ComponentsCache.GetOrCreate(project.Name, () => LoadFromProject(project));
        return dotrushComponents.AddRanges(roslynCoreComponents, roslynCSharpComponents, projectProviders).ToImmutableArray();
    }

    public List<CodeFixProvider> LoadFromAssembly(string assemblyName) {
        var result = new List<CodeFixProvider>();
        var assemblyTypes = ReflectionExtensions.LoadAssembly(assemblyName);
        if (assemblyTypes == null)
            return result;

        var providersInfo = assemblyTypes.Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(CodeFixProvider)));
        foreach (var providerInfo in providersInfo) {
            try {
                var attribute = providerInfo.GetCustomAttribute<ExportCodeFixProviderAttribute>();
                if (attribute == null) {
                    currentClassLogger.Debug($"Skipping code fix provider '{providerInfo.Name}' because it is missing the '${nameof(ExportCodeFixProviderAttribute)}'");
                    continue;
                }
                if (Activator.CreateInstance(providerInfo.AsType()) is not CodeFixProvider instance) {
                    currentClassLogger.Error($"Instance of code fix provider '{providerInfo.Name}' is null");
                    continue;
                }
                result.Add(instance);
                currentClassLogger.Debug($"Loaded code fix provider: {instance}");
            } catch (Exception ex) {
                currentClassLogger.Error($"Creating instance of '{providerInfo.Name}' failed, error: {ex}");
            }
        }
        currentClassLogger.Debug($"Loaded {result.Count} codeFixProviders form assembly '{assemblyName}'");
        return result;
    }
    public List<CodeFixProvider> LoadFromProject(Project project) {
        var analyzerReferenceAssemblies = project.AnalyzerReferences.Select(it => it.FullPath);
        var result = analyzerReferenceAssemblies.SelectMany(it => LoadFromAssembly(it ?? string.Empty)).ToList();
        currentClassLogger.Debug($"Loaded {result.Count} codeFixProviders from project '{project.Name}'");
        return result;
    }
    public List<CodeFixProvider> LoadFromDotRush() {
        return new List<CodeFixProvider>();
    }
}