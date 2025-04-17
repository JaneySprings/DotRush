using System.Collections.Immutable;
using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotRush.Roslyn.CodeAnalysis.Components;

public class DiagnosticAnalyzersLoader : IComponentLoader<DiagnosticAnalyzer> {
    public MemoryCache<DiagnosticAnalyzer> ComponentsCache { get; } = new MemoryCache<DiagnosticAnalyzer>();
    private readonly CurrentClassLogger currentClassLogger = new CurrentClassLogger(nameof(DiagnosticAnalyzersLoader));

    public ImmutableArray<DiagnosticAnalyzer> GetComponents(Project? project = null) {
        var dotrushComponents = ComponentsCache.GetOrCreate(KnownAssemblies.DotRushCodeAnalysis, () => LoadFromDotRush());
        var roslynCoreComponents = ComponentsCache.GetOrCreate(KnownAssemblies.CommonFeaturesAssemblyName, () => LoadFromAssembly(KnownAssemblies.CommonFeaturesAssemblyName));
        var roslynCSharpComponents = ComponentsCache.GetOrCreate(KnownAssemblies.CSharpFeaturesAssemblyName, () => LoadFromAssembly(KnownAssemblies.CSharpFeaturesAssemblyName));
        if (project == null)
            return dotrushComponents.AddRanges(roslynCoreComponents, roslynCSharpComponents).ToImmutableArray();
            // return dotrushComponents.ToImmutableArray();

        var projectProviders = ComponentsCache.GetOrCreate(project.Name, () => LoadFromProject(project));
        return dotrushComponents.AddRanges(roslynCoreComponents, roslynCSharpComponents, projectProviders).ToImmutableArray();
        // return dotrushComponents.Concat(projectProviders).ToImmutableArray();
    }
    public ImmutableArray<DiagnosticAnalyzer> GetSuppressors(Project? project = null) {
        return GetComponents(project).Where(it => it is DiagnosticSuppressor).ToImmutableArray();
    }

    public List<DiagnosticAnalyzer> LoadFromProject(Project project) {
        var result = new List<DiagnosticAnalyzer>();
        foreach (var reference in project.AnalyzerReferences)
            foreach (var analyzer in reference.GetAnalyzers(project.Language)) {
                result.Add(analyzer);
                currentClassLogger.Debug($"Loaded analyzer: {analyzer}");
            }

        currentClassLogger.Debug($"Loaded {result.Count} analyzers from project '{project.Name}'");
        return result;
    }
    public List<DiagnosticAnalyzer> LoadFromAssembly(string assemblyName) {
        var result = new List<DiagnosticAnalyzer>();
        var assemblyTypes = ReflectionExtensions.LoadAssembly(assemblyName);
        if (assemblyTypes == null)
            return result;

        var analyzersInfo = assemblyTypes.Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(DiagnosticAnalyzer)));
        foreach (var analyzerInfo in analyzersInfo) {
            try {
                if (Activator.CreateInstance(analyzerInfo.AsType()) is not DiagnosticAnalyzer instance) {
                    currentClassLogger.Error($"Instance of analyzer '{analyzerInfo.Name}' is null");
                    continue;
                }
                result.Add(instance);
                currentClassLogger.Debug($"Loaded analyzer: {instance}");
            } catch (Exception ex) {
                currentClassLogger.Error($"Creating instance of analyzer '{analyzerInfo.Name}' failed, error: {ex}");
            }
        }
        currentClassLogger.Debug($"Loaded {result.Count} analyzers form assembly '{assemblyName}'");
        return result;
    }
    public List<DiagnosticAnalyzer> LoadFromDotRush() {
        return new List<DiagnosticAnalyzer>();
    }
}