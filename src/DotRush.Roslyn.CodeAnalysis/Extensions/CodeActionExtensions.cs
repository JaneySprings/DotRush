using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DotRush.Roslyn.CodeAnalysis.Extensions;

public static class CodeActionExtensions {
    public static int GetUniqueId(this CodeAction codeAction) {
        var id = codeAction.EquivalenceKey ?? codeAction.Title;
        return id.GetHashCode();
    }

    public static async Task<CodeAction?> TryGetFixAsync(this FixAllProvider provider, FixAllContext context) {
        try {
            // Some providers can throw an exceptions. Wait for result here and try-catch it.
            return await provider.GetFixAsync(context);
        } catch (Exception e) {
            CurrentSessionLogger.Error(e);
            return null;
        }
    }
    public static async Task RegisterFixAllCodeFixesAsync(this CodeFixProvider provider, Document document, DiagnosticContext? diagnosticContext, FixAllContext.DiagnosticProvider host, Action<CodeAction> registerCodeFix, CancellationToken cancellationToken) {
        var fixAllProvider = provider.GetFixAllProvider();
        if (fixAllProvider == null || diagnosticContext == null)
            return;

        foreach (var scope in fixAllProvider.GetSupportedFixAllScopes()) {
            if (!diagnosticContext.IsSupportedScope(scope))
                continue;

            var equivalenceKey = $"{diagnosticContext.Id}_{scope}";
            var codeAction = await fixAllProvider.TryGetFixAsync(new FixAllContext(document, provider, scope, equivalenceKey, new[] { diagnosticContext.Id }, host, cancellationToken));
            if (codeAction == null)
                return;

            registerCodeFix.Invoke(codeAction);
        }
    }

    private static bool IsSupportedScope(this DiagnosticContext context, FixAllScope scope) {
        switch (context.Scope) {
            case AnalysisScope.Document: return scope == FixAllScope.Document;
            case AnalysisScope.Project: return scope == FixAllScope.Document || scope == FixAllScope.Project;
            case AnalysisScope.Solution: return scope == FixAllScope.Document || scope == FixAllScope.Project || scope == FixAllScope.Solution;
            default: return false;
        }
    }
}