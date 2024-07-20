using DotRush.Roslyn.CodeAnalysis.Components;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DotRush.Roslyn.CodeAnalysis;

public class CodeActionHost {
    private readonly CodeFixProvidersLoader codeFixProvidersLoader = new CodeFixProvidersLoader();
    private readonly CodeRefactoringProvidersLoader codeRefactoringsProviderProvider = new CodeRefactoringProvidersLoader();

    public IEnumerable<CodeFixProvider>? GetCodeFixProvidersForDiagnosticId(string? diagnosticId, Project? project) {
        if (diagnosticId == null)
            return null;
        return codeFixProvidersLoader.GetComponents(project).Where(x => x.FixableDiagnosticIds.CanFixDiagnostic(diagnosticId));
    }
}
