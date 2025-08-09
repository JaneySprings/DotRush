using System.Collections.Immutable;
using DotRush.Roslyn.CodeAnalysis.Components;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace DotRush.Roslyn.CodeAnalysis;

public class CodeActionHost {
    private readonly CodeFixProvidersLoader codeFixProvidersLoader;
    private readonly CodeRefactoringProvidersLoader codeRefactoringsProviderProvider;

    public CodeActionHost(IAdditionalComponentsProvider additionalComponentsProvider) {
        codeFixProvidersLoader = new CodeFixProvidersLoader(additionalComponentsProvider);
        codeRefactoringsProviderProvider = new CodeRefactoringProvidersLoader(additionalComponentsProvider);
    }

    public IEnumerable<CodeFixProvider>? GetCodeFixProvidersForDiagnosticId(string? diagnosticId, Project project) {
        if (diagnosticId == null)
            return null;
        return codeFixProvidersLoader.GetComponents(project).Where(x => CanFixDiagnostic(x.FixableDiagnosticIds, diagnosticId));
    }
    public IEnumerable<CodeRefactoringProvider>? GetCodeRefactoringProvidersForProject(Project project) {
        return codeRefactoringsProviderProvider.GetComponents(project);
    }

    private static bool CanFixDiagnostic(ImmutableArray<string> array, string item) {
        if (item == "CS8019")
            return array.Contains("RemoveUnnecessaryImportsFixable");

        return array.Contains(item);
    }
}
