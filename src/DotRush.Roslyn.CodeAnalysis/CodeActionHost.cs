using DotRush.Roslyn.CodeAnalysis.Components;
using DotRush.Roslyn.CodeAnalysis.Extensions;
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
        return codeFixProvidersLoader.GetComponents(project).Where(x => x.FixableDiagnosticIds.CanFixDiagnostic(diagnosticId));
    }
    public IEnumerable<CodeRefactoringProvider>? GetCodeRefactoringProvidersForProject(Project project) {
        return codeRefactoringsProviderProvider.GetComponents(project);
    }
}
