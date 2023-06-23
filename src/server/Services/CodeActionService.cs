using System.Reflection;
using DotRush.Server.Extensions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DotRush.Server.Services;

public class CodeActionService {
    public FileCodeActions CodeActions { get; }
    public HashSet<CodeFixProvider> CodeFixProviders { get; }

    public CodeActionService() {
        CodeActions = new FileCodeActions();
        CodeFixProviders = new HashSet<CodeFixProvider>();
        var providersLocations = new List<string>() {
            "Microsoft.CodeAnalysis.CSharp.Features",
            "Microsoft.CodeAnalysis.CSharp.Workspaces",
            "Microsoft.CodeAnalysis.Workspaces",
            "Microsoft.CodeAnalysis.Features"
        };

        foreach (var location in providersLocations)
            AddCodeFixesWithAssemblyName(location);

        if (!Directory.Exists(LanguageServer.AnalyzersLocation))
            return;

        foreach (var codefixPath in Directory.GetFiles(LanguageServer.AnalyzersLocation, "*.dll"))
            AddCodeFixesWithAssemblyPath(codefixPath);
    }

    private void AddCodeFixesWithAssemblyName(string assemblyName) {
        AddCodeFixes(Assembly.Load(assemblyName));
    }
    private void AddCodeFixesWithAssemblyPath(string assemblyPath) {
        AddCodeFixes(Assembly.LoadFrom(assemblyPath));
    }
    private void AddCodeFixes(Assembly assembly) {
        var providers = assembly.DefinedTypes
            .Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(CodeFixProvider)))
            .Select(x => {
                try {
                    var attribute = x.GetCustomAttribute<ExportCodeFixProviderAttribute>();
                    if (attribute == null) {
                        LoggingService.Instance.LogMessage($"Skipping code fix provider '{x.AsType()}' because it is missing the ExportCodeFixProviderAttribute.");
                        return null;
                    }

                    if (attribute.Languages == null) {
                        LoggingService.Instance.LogMessage($"Skipping code fix provider '{x.AsType()}' because its language '{attribute.Languages?.FirstOrDefault()}' doesn't specified.");
                        return null;
                    }

                    return Activator.CreateInstance(x.AsType()) as CodeFixProvider;
                } catch (Exception ex) {
                    LoggingService.Instance.LogError($"Creating instance of code fix provider '{x.AsType()}' failed, error: {ex}");
                    return null;
                }
            }).Where(x => x != null);
        
        foreach (var provider in providers)
            CodeFixProviders.Add(provider!);
    }
}