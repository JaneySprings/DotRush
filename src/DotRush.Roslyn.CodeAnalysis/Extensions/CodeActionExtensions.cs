using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using ApplyChangesOperation = Microsoft.CodeAnalysis.CodeActions.ApplyChangesOperation;

namespace DotRush.Roslyn.CodeAnalysis.Extensions;

public static class CodeActionExtensions {
    public static int GetUniqueId(this CodeAction codeAction) {
        var id = codeAction.EquivalenceKey ?? codeAction.Title;
        return id.GetHashCode();
    }

    public static void RegisterFixAllCodeFixesAsync(this CodeFixProvider codeFixProvider, Document document, string diagnosticId, string title, string? equivalenceKey, Func<Document?> getCurrentDocument, FixAllContext.DiagnosticProvider diagnosticProvider, Action<CodeAction> registerCodeFix, CancellationToken cancellationToken) {
        var fixAllProvider = codeFixProvider.GetFixAllProvider();
        if (fixAllProvider == null)
            return;

        foreach (var scope in fixAllProvider.GetSupportedFixAllScopes()) {
            if (cancellationToken.IsCancellationRequested)
                return;
            if (scope is not FixAllScope.Document and not FixAllScope.Project and not FixAllScope.Solution)
                continue;

            var fixAllTitle = $"Fix all '{title}' in {scope}";
            var fixAllAction = CodeAction.Create(
                fixAllTitle,
                createChangedSolution: currentCancellationToken => GetChangedSolutionAsync(document, codeFixProvider, fixAllProvider, scope, diagnosticId, equivalenceKey, getCurrentDocument, diagnosticProvider, currentCancellationToken),
                equivalenceKey: (equivalenceKey ?? fixAllTitle) + "-" + scope
            );
            registerCodeFix.Invoke(fixAllAction);
        }
    }

    private static async Task<Solution> GetChangedSolutionAsync(Document originalDocument, CodeFixProvider codeFixProvider, FixAllProvider fixAllProvider, FixAllScope scope, string diagnosticId, string? equivalenceKey, Func<Document?> getCurrentDocument, FixAllContext.DiagnosticProvider diagnosticProvider, CancellationToken cancellationToken) {
        var currentDocument = getCurrentDocument() ?? originalDocument;
        var fixAllContext = new FixAllContext(currentDocument, codeFixProvider, scope, equivalenceKey, new[] { diagnosticId }, diagnosticProvider, cancellationToken);
        var action = await fixAllProvider.GetFixAsync(fixAllContext).ConfigureAwait(false);
        if (action == null)
            return currentDocument.Project.Solution;

        var operations = await action.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
        var applyChangesOperation = operations.OfType<ApplyChangesOperation>().LastOrDefault();
        return applyChangesOperation?.ChangedSolution ?? currentDocument.Project.Solution;
    }
}
