using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class FixAllCodeAction : CodeAction {
    private readonly FixAllProvider fixAllProvider;
    private readonly FixAllContext fixAllContext;
    private readonly string title;
    private readonly string equivalenceKey;

    public override string Title => title;
    public override string? EquivalenceKey => equivalenceKey;

    public FixAllCodeAction(FixAllProvider fixAllProvider, FixAllContext fixAllContext, string title) {
        this.fixAllProvider = fixAllProvider;
        this.fixAllContext = fixAllContext;
        this.title = title;
        this.equivalenceKey = fixAllContext.CodeActionEquivalenceKey + "-" + fixAllContext.Scope.ToString();
    }

    protected override async Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(CancellationToken cancellationToken) {
        var action = await fixAllProvider.GetFixAsync(fixAllContext).ConfigureAwait(false);
        if (action == null)
            return Enumerable.Empty<CodeActionOperation>();

        return await action.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
    }
}
