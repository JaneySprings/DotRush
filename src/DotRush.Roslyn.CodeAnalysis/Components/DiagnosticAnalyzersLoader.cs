using System.Collections.Immutable;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotRush.Roslyn.CodeAnalysis.Components;

public class DiagnosticAnalyzersLoader : IComponentLoader<DiagnosticAnalyzer> {
    private readonly CurrentClassLogger currentClassLogger;
    private readonly IAdditionalComponentsProvider additionalComponentsProvider;

    public MemoryCache<DiagnosticAnalyzer> ComponentsCache { get; }

    public DiagnosticAnalyzersLoader(IAdditionalComponentsProvider additionalComponentsProvider) {
        this.additionalComponentsProvider = additionalComponentsProvider;
        currentClassLogger = new CurrentClassLogger(nameof(DiagnosticAnalyzersLoader));
        ComponentsCache = new MemoryCache<DiagnosticAnalyzer>();
    }

    public ImmutableArray<DiagnosticAnalyzer> GetComponents(Project project) {
        return ComponentsCache.GetOrCreate(project.Name, () => {
            var result = new List<DiagnosticAnalyzer>();
            result.AddRange(LoadFromDotRush());
            result.AddRange(LoadFromAssembly(KnownAssemblies.CommonFeaturesAssemblyName));
            result.AddRange(LoadFromAssembly(KnownAssemblies.CSharpFeaturesAssemblyName));

            result.AddRange(LoadFromProject(project));

            if (additionalComponentsProvider.IsEnabled) {
                foreach (var assemblyName in additionalComponentsProvider.GetAdditionalAssemblies())
                    result.AddRange(LoadFromAssembly(assemblyName));
            }

            return result;
        }).ToImmutableArray();
    }
    public ImmutableArray<DiagnosticAnalyzer> GetSuppressors(Project project) {
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