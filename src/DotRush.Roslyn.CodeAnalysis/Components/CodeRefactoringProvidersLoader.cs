using System.Collections.Immutable;
using System.Reflection;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Embedded.Refactorings;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace DotRush.Roslyn.CodeAnalysis.Components;

public class CodeRefactoringProvidersLoader : IComponentLoader<CodeRefactoringProvider> {
    private readonly CurrentClassLogger currentClassLogger;
    private readonly IAdditionalComponentsProvider additionalComponentsProvider;

    public MemoryCache<CodeRefactoringProvider> ComponentsCache { get; }

    public CodeRefactoringProvidersLoader(IAdditionalComponentsProvider additionalComponentsProvider) {
        this.additionalComponentsProvider = additionalComponentsProvider;
        currentClassLogger = new CurrentClassLogger(nameof(CodeRefactoringProvidersLoader));
        ComponentsCache = new MemoryCache<CodeRefactoringProvider>();
    }

    public ImmutableArray<CodeRefactoringProvider> GetComponents(Project project) {
        return ComponentsCache.GetOrCreate(project.Name, () => {
            var result = new List<CodeRefactoringProvider>();
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
            new GenerateDescriptionRefactoringProvider(),
        };
    }
}