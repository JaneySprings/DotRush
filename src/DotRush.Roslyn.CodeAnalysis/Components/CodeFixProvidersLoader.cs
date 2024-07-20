using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Reflection;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Common;
using DotRush.Roslyn.Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DotRush.Roslyn.CodeAnalysis.Components;

public class CodeFixProvidersLoader : IComponentLoader<CodeFixProvider> {
    public MemoryCache<CodeFixProvider> ComponentsCache { get; } = new MemoryCache<CodeFixProvider>();

    public ImmutableArray<CodeFixProvider> GetComponents(Project? project = null) {
        var embeddedProviders = ComponentsCache.GetOrCreate(KnownAssemblies.CSharpFeaturesAssemblyName, () => LoadFromAssembly(KnownAssemblies.CSharpFeaturesAssemblyName));
        if (project == null)
            return embeddedProviders.ToImmutableArray();

        var projectProviders = ComponentsCache.GetOrCreate(project.Name, () => LoadFromProject(project));
        return embeddedProviders.Concat(projectProviders).ToImmutableArray();
    }

    public ReadOnlyCollection<CodeFixProvider> LoadFromAssembly(string assemblyName) {
        var result = new List<CodeFixProvider>();
        var assembly = ReflectionExtensions.LoadAssembly(assemblyName);
        if (assembly == null)
            return new ReadOnlyCollection<CodeFixProvider>(result);

        var providersInfo = assembly.DefinedTypes.Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(CodeFixProvider)));
        foreach (var providerInfo in providersInfo) {
            try {
                var attribute = providerInfo.GetCustomAttribute<ExportCodeFixProviderAttribute>();
                if (attribute == null) {
                    CurrentSessionLogger.Debug($"Skipping code fix provider '{providerInfo.Name}' because it is missing the 'ExportCodeFixProviderAttribute'");
                    continue;
                }
                if (Activator.CreateInstance(providerInfo.AsType()) is not CodeFixProvider instance) {
                    CurrentSessionLogger.Error($"Instance of code fix provider '{providerInfo.Name}' is null");
                    continue;
                }
                result.Add(instance);
                CurrentSessionLogger.Debug($"Loaded code fix provider: {instance}");
            } catch (Exception ex) {
                CurrentSessionLogger.Error($"Creating instance of analyzer '{providerInfo.Name}' failed, error: {ex}");
            }
        }
        CurrentSessionLogger.Debug($"Loaded {result.Count} codeFixProviders form assembly '{assembly.FullName}'");
        return new ReadOnlyCollection<CodeFixProvider>(result);
    }
    public ReadOnlyCollection<CodeFixProvider> LoadFromProject(Project project) {
        var analyzerReferenceAssemblies = project.AnalyzerReferences.Select(it => it.FullPath);
        var result = analyzerReferenceAssemblies.SelectMany(it => LoadFromAssembly(it ?? string.Empty)).ToArray();
        return new ReadOnlyCollection<CodeFixProvider>(result);
    }
}