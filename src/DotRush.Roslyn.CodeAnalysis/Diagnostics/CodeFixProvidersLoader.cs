using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Reflection;
using DotRush.Roslyn.Common;
using DotRush.Roslyn.Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

public class CodeFixProvidersLoader : IComponentLoader<CodeFixProvider> {
    private readonly Dictionary<string, CodeFixProvider> codeFixProvidersCache = new Dictionary<string, CodeFixProvider>();

    public void InitializeEmbeddedComponents() {
        var csharpEmbeddedProviders = LoadFromAssembly(Assembly.Load(KnownAssemblies.CSharpFeaturesAssemblyName));
        foreach (var provider in csharpEmbeddedProviders)
            codeFixProvidersCache.TryAdd(provider.ToString()!, provider);
    }
    public ReadOnlyCollection<CodeFixProvider> LoadFromAssembly(Assembly assembly) {
        var result = new List<CodeFixProvider>();
        var providersInfo = assembly.DefinedTypes.Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(CodeFixProvider)));
        foreach (var providerInfo in providersInfo) {
            try {
                var attribute = providerInfo.GetCustomAttribute<ExportCodeFixProviderAttribute>();
                if (attribute == null) {
                    CurrentSessionLogger.Debug($"Skipping code fix provider '{providerInfo.Name}' because it is missing the 'ExportCodeFixProviderAttribute'");
                    continue;
                }
                if (Activator.CreateInstance(providerInfo.AsType()) is not CodeFixProvider instance) {
                    CurrentSessionLogger.Error($"Instance of analyzer '{providerInfo.Name}' is null");
                    continue;
                }
                result.Add(instance);
            } catch (Exception ex) {
                CurrentSessionLogger.Error($"Creating instance of analyzer '{providerInfo.Name}' failed, error: {ex}");
            }
        }
        CurrentSessionLogger.Debug($"Loaded {result.Count} codeFixProviders form assembly '{assembly.FullName}'");
        return new ReadOnlyCollection<CodeFixProvider>(result);
    }
    public ReadOnlyCollection<CodeFixProvider> LoadFromProject(Project project) {
        var analyzerReferenceAssemblies = project.AnalyzerReferences.Select(it => it.FullPath).Where(it => File.Exists(it));
        var result = analyzerReferenceAssemblies.SelectMany(it => LoadFromAssembly(Assembly.LoadFrom(it!))).ToArray();

        CurrentSessionLogger.Debug($"Loaded {result.Length} codeFixProviders form project '{project.Name}'");
        return new ReadOnlyCollection<CodeFixProvider>(result);
    }
    public ImmutableArray<CodeFixProvider> GetComponents(Project? project = null) {
        if (project == null)
            return codeFixProvidersCache.Values.ToImmutableArray();

        var projectProviders = LoadFromProject(project);
        return projectProviders.Concat(codeFixProvidersCache.Values).ToImmutableArray();
    }
}