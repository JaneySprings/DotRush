using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using DotRush.Server.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DotRush.Server.Services;

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
    }
    public void AddProjectProviders(Project project) {
        if (projectCodeFixProviders.ContainsKey(project.Id))
            return;

        var analyzerReferenceAssemblies = project.AnalyzerReferences
            .Where(x => !string.IsNullOrEmpty(x.FullPath) && File.Exists(x.FullPath))
            .Select(x => x.FullPath);

        foreach (var assemblyPath in analyzerReferenceAssemblies) {
            var assembly = Assembly.LoadFrom(assemblyPath!);
            projectCodeFixProviders.Add(project.Id, CreateCodeFixProviders(assembly));
        }
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
                            Debug.WriteLine($"Skipping code fix provider '{x.AsType()}' because it is missing the ExportCodeFixProviderAttribute.");
                            return null;
                        }

                        return Activator.CreateInstance(x.AsType()) as CodeFixProvider;
                    } catch (Exception ex) {
                        Debug.WriteLine($"Creating instance of code fix provider '{x.AsType()}' failed, error: {ex}");
                        return null;
                    }
                })
                .Where(x => x != null)!;
        });
    }
}