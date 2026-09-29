using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Embedded.Refactorings;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RemoveBracesRefactoringProvider)), Shared]
public class RemoveBracesRefactoringProvider : CodeRefactoringProvider {
    public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null || context.Span.Start >= root.FullSpan.End)
            return;

        var token = root.FindToken(context.Span.Start);
        var block = token.Parent as BlockSyntax ?? (token.IsKeyword() ? GetEmbeddedStatement(token.Parent) as BlockSyntax : null);
        if (block == null || block.Statements.Count != 1 || GetEmbeddedStatement(block.Parent) != block)
            return;

        var statement = block.Statements[0];
        if (statement is LocalDeclarationStatementSyntax || statement is LocalFunctionStatementSyntax || statement is LabeledStatementSyntax)
            return;

        var previousToken = block.OpenBraceToken.GetPreviousToken();
        var nextToken = block.CloseBraceToken.GetNextToken();
        if (nextToken.IsKind(SyntaxKind.ElseKeyword) && EndsWithIfStatement(statement))
            return;

        var trivia = previousToken.TrailingTrivia
            .Concat(block.OpenBraceToken.LeadingTrivia).Concat(block.OpenBraceToken.TrailingTrivia)
            .Concat(block.CloseBraceToken.LeadingTrivia).Concat(block.CloseBraceToken.TrailingTrivia);
        if (trivia.Any(it => !it.IsKind(SyntaxKind.WhitespaceTrivia) && !it.IsKind(SyntaxKind.EndOfLineTrivia)))
            return;

        context.RegisterRefactoring(CodeAction.Create(
            "Remove braces",
            c => RemoveBracesAsync(context.Document, block, c),
            equivalenceKey: nameof(RemoveBracesRefactoringProvider)
        ));
    }
    private static async Task<Document> RemoveBracesAsync(Document document, BlockSyntax block, CancellationToken cancellationToken) {
        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var statement = block.Statements[0];
        var openLine = sourceText.Lines.GetLineFromPosition(block.OpenBraceToken.SpanStart);
        var closeLine = sourceText.Lines.GetLineFromPosition(block.CloseBraceToken.SpanStart);
        var previousToken = block.OpenBraceToken.GetPreviousToken();
        var nextToken = block.CloseBraceToken.GetNextToken();

        var openChange = statement.FullSpan.Start <= openLine.End
            ? new TextChange(TextSpan.FromBounds(previousToken.Span.End, statement.FullSpan.Start), " ")
            : new TextChange(TextSpan.FromBounds(previousToken.Span.End, openLine.End), string.Empty);

        TextSpan closeSpan;
        if (statement.Span.End > closeLine.Start)
            closeSpan = TextSpan.FromBounds(statement.GetTrailingTrivia().All(it => it.IsKind(SyntaxKind.WhitespaceTrivia)) ? statement.Span.End : block.CloseBraceToken.SpanStart, block.CloseBraceToken.Span.End);
        else if (!nextToken.IsKind(SyntaxKind.None) && nextToken.SpanStart < closeLine.EndIncludingLineBreak)
            closeSpan = TextSpan.FromBounds(block.CloseBraceToken.SpanStart, nextToken.SpanStart);
        else
            closeSpan = TextSpan.FromBounds(closeLine.Start, closeLine.EndIncludingLineBreak);

        return document.WithText(sourceText.WithChanges(openChange, new TextChange(closeSpan, string.Empty)));
    }

    private static StatementSyntax? GetEmbeddedStatement(SyntaxNode? node) {
        return node switch {
            IfStatementSyntax ifStatement => ifStatement.Statement,
            ElseClauseSyntax elseClause => elseClause.Statement,
            ForStatementSyntax forStatement => forStatement.Statement,
            CommonForEachStatementSyntax forEachStatement => forEachStatement.Statement,
            WhileStatementSyntax whileStatement => whileStatement.Statement,
            DoStatementSyntax doStatement => doStatement.Statement,
            UsingStatementSyntax usingStatement => usingStatement.Statement,
            LockStatementSyntax lockStatement => lockStatement.Statement,
            FixedStatementSyntax fixedStatement => fixedStatement.Statement,
            _ => null
        };
    }
    // The following 'else' clause will be attached to this statement without braces
    private static bool EndsWithIfStatement(StatementSyntax? statement) {
        while (statement != null) {
            if (statement is IfStatementSyntax ifStatement) {
                if (ifStatement.Else == null)
                    return true;
                statement = ifStatement.Else.Statement;
                continue;
            }
            statement = statement is DoStatementSyntax ? null : GetEmbeddedStatement(statement);
        }
        return false;
    }
}