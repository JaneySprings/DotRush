using System.Collections.Immutable;
using System.Composition;
using DotRush.Roslyn.CodeAnalysis.Embedded.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Embedded.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseIsNullCheckCodeFixProvider)), Shared]
public class UseIsNullCheckCodeFixProvider : CodeFixProvider {
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UseIsNullCheckDiagnosticAnalyzer.DiagnosticId);

    public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        foreach (var diagnostic in context.Diagnostics) {
            if (root?.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not BinaryExpressionSyntax binaryExpression)
                continue;

            var pattern = binaryExpression.IsKind(SyntaxKind.EqualsExpression) ? "is null" : "is not null";
            context.RegisterCodeFix(CodeAction.Create(
                $"Use '{pattern}' check",
                c => UsePatternMatchingAsync(context.Document, binaryExpression, pattern, c),
                equivalenceKey: nameof(UseIsNullCheckCodeFixProvider)
            ), diagnostic);
        }
    }
    public sealed override FixAllProvider? GetFixAllProvider() {
        return WellKnownFixAllProviders.BatchFixer;
    }

    private static async Task<Document> UsePatternMatchingAsync(Document document, BinaryExpressionSyntax binaryExpression, string pattern, CancellationToken cancellationToken) {
        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var textChange = binaryExpression.Left.IsKind(SyntaxKind.NullLiteralExpression)
            ? new TextChange(binaryExpression.Span, $"{binaryExpression.Right.WithoutTrivia()} {pattern}")
            : new TextChange(TextSpan.FromBounds(binaryExpression.OperatorToken.SpanStart, binaryExpression.Right.Span.End), pattern);

        return document.WithText(sourceText.WithChanges(textChange));
    }
}