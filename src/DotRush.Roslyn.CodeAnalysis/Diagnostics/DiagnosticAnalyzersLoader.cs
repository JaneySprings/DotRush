using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Reflection;
using DotRush.Roslyn.Common;
using DotRush.Roslyn.Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

public class DiagnosticAnalyzersLoader : IComponentLoader<DiagnosticAnalyzer> {
    private readonly Dictionary<string, DiagnosticAnalyzer> diagnosticAnalyzersCache = new Dictionary<string, DiagnosticAnalyzer>();

    public void InitializeEmbeddedComponents() {
        var csharpEmbeddedAnalyzers = LoadFromAssembly(Assembly.Load(KnownAssemblies.CSharpFeaturesAssemblyName));
        foreach (var analyzer in csharpEmbeddedAnalyzers)
            diagnosticAnalyzersCache.TryAdd(analyzer.ToString(), analyzer);
    }
    public ReadOnlyCollection<DiagnosticAnalyzer> LoadFromProject(Project project) {
        var result = new List<DiagnosticAnalyzer>();
        foreach (var reference in project.AnalyzerReferences)
            result.AddRange(reference.GetAnalyzers(project.Language));

        CurrentSessionLogger.Debug($"Found {result.Count} analyzers in the project '{project.Name}'");
        return new ReadOnlyCollection<DiagnosticAnalyzer>(result);
    }
    public ReadOnlyCollection<DiagnosticAnalyzer> LoadFromAssembly(Assembly assembly) {
        var result = new List<DiagnosticAnalyzer>();
        var analyzersInfo = assembly.DefinedTypes.Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(DiagnosticAnalyzer)));
        foreach (var analyzerInfo in analyzersInfo) {
            try {
                if (Activator.CreateInstance(analyzerInfo.AsType()) is not DiagnosticAnalyzer instance) {
                    CurrentSessionLogger.Error($"Instance of analyzer '{analyzerInfo.Name}' is null");
                    continue;
                }
                result.Add(instance);
            } catch (Exception ex) {
                CurrentSessionLogger.Error($"Creating instance of analyzer '{analyzerInfo.Name}' failed, error: {ex}");
            }
        }
        CurrentSessionLogger.Debug($"Loaded {result.Count} analyzers form assembly '{assembly.FullName}'");
        return new ReadOnlyCollection<DiagnosticAnalyzer>(result);
    }
    public ImmutableArray<DiagnosticAnalyzer> GetComponents(Project? project = null) {
        if (diagnosticAnalyzersCache.Count == 0)
            InitializeEmbeddedComponents();

        if (project == null)
            return diagnosticAnalyzersCache.Values.ToImmutableArray();

        var projectAnalyzers = LoadFromProject(project);
        return projectAnalyzers.Concat(diagnosticAnalyzersCache.Values).ToImmutableArray();
    }
}