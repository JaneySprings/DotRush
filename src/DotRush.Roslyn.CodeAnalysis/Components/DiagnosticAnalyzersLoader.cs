using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Reflection;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Common;
using DotRush.Roslyn.Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotRush.Roslyn.CodeAnalysis.Components;

public class DiagnosticAnalyzersLoader : IComponentLoader<DiagnosticAnalyzer> {
    public MemoryCache<DiagnosticAnalyzer> ComponentsCache { get; } = new MemoryCache<DiagnosticAnalyzer>();

    public ImmutableArray<DiagnosticAnalyzer> GetComponents(Project? project = null) {
        if (project == null)
            return ImmutableArray<DiagnosticAnalyzer>.Empty;

        return ComponentsCache.GetOrCreate(project.Name, () => LoadFromProject(project)).ToImmutableArray();
    }

    public ReadOnlyCollection<DiagnosticAnalyzer> LoadFromProject(Project project) {
        var result = new List<DiagnosticAnalyzer>();
        foreach (var reference in project.AnalyzerReferences)
            foreach (var analyzer in reference.GetAnalyzers(project.Language)) {
                result.Add(analyzer);
                CurrentSessionLogger.Debug($"Loaded analyzer: {analyzer}");
            }

        CurrentSessionLogger.Debug($"Found {result.Count} analyzers in the project '{project.Name}'");
        return new ReadOnlyCollection<DiagnosticAnalyzer>(result);
    }
    public ReadOnlyCollection<DiagnosticAnalyzer> LoadFromAssembly(string assemblyName) {
        var result = new List<DiagnosticAnalyzer>();
        var assemblyTypes = ReflectionExtensions.LoadAssembly(assemblyName);
        if (assemblyTypes == null)
            return new ReadOnlyCollection<DiagnosticAnalyzer>(result);

        var analyzersInfo = assemblyTypes.Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(DiagnosticAnalyzer)));
        foreach (var analyzerInfo in analyzersInfo) {
            try {
                if (Activator.CreateInstance(analyzerInfo.AsType()) is not DiagnosticAnalyzer instance) {
                    CurrentSessionLogger.Error($"Instance of analyzer '{analyzerInfo.Name}' is null");
                    continue;
                }
                result.Add(instance);
                CurrentSessionLogger.Debug($"Loaded analyzer: {instance}");
            } catch (Exception ex) {
                CurrentSessionLogger.Error($"Creating instance of analyzer '{analyzerInfo.Name}' failed, error: {ex}");
            }
        }
        CurrentSessionLogger.Debug($"Loaded {result.Count} analyzers form assembly '{assemblyName}'");
        return new ReadOnlyCollection<DiagnosticAnalyzer>(result);
    }
}