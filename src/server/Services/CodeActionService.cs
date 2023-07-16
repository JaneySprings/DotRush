using System.Collections.Immutable;
using System.Reflection;
using DotRush.Server.Containers;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DotRush.Server.Services;

public class CodeActionService {
    public CodeActionCollection CodeActions { get; }
    public ImmutableArray<CodeFixProvider> CodeFixProviders { get; private set; }

    private readonly string[] embeddedAnalyzerReferences = new string[] {
        "Microsoft.CodeAnalysis.CSharp.Features",
        "Microsoft.CodeAnalysis.CSharp.Workspaces",
        "Microsoft.CodeAnalysis.Workspaces",
        "Microsoft.CodeAnalysis.Features"
    };

    public CodeActionService() {
        CodeActions = new CodeActionCollection();
        CodeFixProviders = ImmutableArray<CodeFixProvider>.Empty;
    }

    public void InitializeCodeFixes() {
        CodeFixProviders = this.embeddedAnalyzerReferences
            .Select(x => Assembly.Load(x))
            .SelectMany(x => x.DefinedTypes)
            .Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(CodeFixProvider)))
            .Select(x => {
                try {
                    var attribute = x.GetCustomAttribute<ExportCodeFixProviderAttribute>();
                    if (attribute == null) {
                        LoggingService.Instance.LogMessage($"Skipping code fix provider '{x.AsType()}' because it is missing the ExportCodeFixProviderAttribute.");
                        return null;
                    }

                    // if (attribute.Languages == null) {
                    //     LoggingService.Instance.LogMessage($"Skipping code fix provider '{x.AsType()}' because its language '{attribute.Languages?.FirstOrDefault()}' doesn't specified.");
                    //     return null;
                    // }

                    return Activator.CreateInstance(x.AsType()) as CodeFixProvider;
                } catch (Exception ex) {
                    LoggingService.Instance.LogError($"Creating instance of code fix provider '{x.AsType()}' failed, error: {ex}");
                    return null;
                }
            })
            .Where(x => x != null)
            .ToImmutableArray()!;
    }
}