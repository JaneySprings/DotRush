using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.CodeAnalysis.Refactorings;
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
