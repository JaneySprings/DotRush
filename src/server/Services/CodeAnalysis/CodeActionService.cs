using System.Collections.Immutable;
using System.Reflection;
using DotRush.Server.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DotRush.Server.Services;

public class CodeActionService {
    private HashSet<CodeFixProviderContainer> codeFixProviders;

    public CodeActionService() {
        this.codeFixProviders = new HashSet<CodeFixProviderContainer>();

        var embeddedProviders = CreateCodeFixProviders(Assembly.Load(LanguageServer.EmbeddedCodeFixAssembly));
        foreach (var provider in embeddedProviders)
            this.codeFixProviders.Add(new CodeFixProviderContainer(provider, LanguageServer.EmbeddedCodeFixAssembly));
    }

    public void AddCodeFixProviders(Project project) {
        var analyzerReferenceAssemblies = project.AnalyzerReferences
            .Select(x => x.FullPath ?? "")
            .Where(x => !string.IsNullOrEmpty(x));

        foreach (var assemblyPath in analyzerReferenceAssemblies) {
            if (this.codeFixProviders.Any(x => x.AssemblyPath == assemblyPath))
                continue;

            var assembly = Assembly.LoadFrom(assemblyPath);
            var providers = CreateCodeFixProviders(assembly);
            foreach (var provider in providers)
                this.codeFixProviders.Add(new CodeFixProviderContainer(provider, assemblyPath, project.FilePath));
        }
    }

    public ImmutableArray<CodeFixProvider> GetCodeFixProviders() {
        return this.codeFixProviders.Select(x => x.Provider).ToImmutableArray();
    }

    private IEnumerable<CodeFixProvider> CreateCodeFixProviders(Assembly assembly) {
        return ServerExtensions.SafeHandler<IEnumerable<CodeFixProvider>>(Enumerable.Empty<CodeFixProvider>(), () => {
            return assembly.DefinedTypes
                .Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(CodeFixProvider)))
                .Select(x => {
                    try {
                        var attribute = x.GetCustomAttribute<ExportCodeFixProviderAttribute>();
                        if (attribute == null) {
                            LoggingService.Instance.LogMessage($"Skipping code fix provider '{x.AsType()}' because it is missing the ExportCodeFixProviderAttribute.");
                            return null;
                        }

                        return Activator.CreateInstance(x.AsType()) as CodeFixProvider;
                    } catch (Exception ex) {
                        LoggingService.Instance.LogError($"Creating instance of code fix provider '{x.AsType()}' failed, error: {ex}");
                        return null;
                    }
                })
                .Where(x => x != null)!;
        });
    }
}