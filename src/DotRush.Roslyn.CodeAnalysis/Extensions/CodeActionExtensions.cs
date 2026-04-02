using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DotRush.Roslyn.CodeAnalysis.Extensions;

public static class CodeActionExtensions {
    public static int GetUniqueId(this CodeAction codeAction) {
        var id = codeAction.EquivalenceKey ?? codeAction.Title;
        return id.GetHashCode();
    }

    public static async Task RegisterFixAllCodeFixesAsync(this CodeFixProvider provider, Document document, string diagnosticId, FixAllContext.DiagnosticProvider host, Action<CodeAction> registerCodeFix, CancellationToken cancellationToken) {
        var fixAllProvider = provider.GetFixAllProvider();
        if (fixAllProvider == null)
            return;

        foreach (var scope in fixAllProvider.GetSupportedFixAllScopes()) {
            // 'FixAllScope.ContainingType' and 'FixAllScope.ContainingMember' are not supported with this constructor'
            if (scope is not FixAllScope.Document and not FixAllScope.Project and not FixAllScope.Solution)
                continue;

            var equivalenceKey = $"{diagnosticId}_{scope}";
            var codeAction = await fixAllProvider.GetFixAsync(new FixAllContext(document, provider, scope, equivalenceKey, new[] { diagnosticId }, host, cancellationToken));
            if (codeAction == null)
                continue;

            registerCodeFix.Invoke(codeAction);
        }
    }
}