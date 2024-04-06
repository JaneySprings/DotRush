using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DotRush.Roslyn.Server.Services;

public class CodeActionService {
    private IEnumerable<CodeFixProvider> embeddedCodeFixProviders;
    private Dictionary<ProjectId, IEnumerable<CodeFixProvider>> projectCodeFixProviders;

    public CodeActionService() {
        embeddedCodeFixProviders = Enumerable.Empty<CodeFixProvider>();
        projectCodeFixProviders = new Dictionary<ProjectId, IEnumerable<CodeFixProvider>>();
    }


    public void InitializeEmbeddedProviders() {
        var codeAnalysisAssembly = Assembly.Load(LanguageServer.CodeAnalysisFeaturesAssembly);
        embeddedCodeFixProviders = CreateCodeFixProviders(codeAnalysisAssembly);
        SessionLogger.LogDebug($"CodeActionService initialized with {embeddedCodeFixProviders.Count()} embedded code fix providers");
    }
    public void AddProjectProviders(Project project) {
        if (projectCodeFixProviders.ContainsKey(project.Id))
            return;

        var analyzerReferenceAssemblies = project.AnalyzerReferences
            .Where(x => !string.IsNullOrEmpty(x.FullPath) && File.Exists(x.FullPath))
            .Select(x => x.FullPath);

        var providers = analyzerReferenceAssemblies.SelectMany(x => CreateCodeFixProviders(Assembly.LoadFrom(x!)));
        projectCodeFixProviders.Add(project.Id, providers);
    }
    public ImmutableArray<CodeFixProvider> GetCodeFixProviders(ProjectId projectId) {
        return embeddedCodeFixProviders.Concat(projectCodeFixProviders[projectId]).ToImmutableArray();
    }


    private IEnumerable<CodeFixProvider> CreateCodeFixProviders(Assembly assembly) {
        return ServerExtensions.SafeHandler<IEnumerable<CodeFixProvider>>(Enumerable.Empty<CodeFixProvider>(), () => {
            return assembly.DefinedTypes
                .Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(CodeFixProvider)))
                .Select(x => {
                    try {
                        var attribute = x.GetCustomAttribute<ExportCodeFixProviderAttribute>();
                        if (attribute == null) {
                            SessionLogger.LogDebug($"Skipping code fix provider '{x.AsType()}' because it is missing the ExportCodeFixProviderAttribute.");
                            return null;
                        }

                        return Activator.CreateInstance(x.AsType()) as CodeFixProvider;
                    } catch (Exception ex) {
                        SessionLogger.LogError($"Creating instance of code fix provider '{x.AsType()}' failed, error: {ex}");
                        return null;
                    }
                })
                .Where(x => x != null)!;
        });
    }
}