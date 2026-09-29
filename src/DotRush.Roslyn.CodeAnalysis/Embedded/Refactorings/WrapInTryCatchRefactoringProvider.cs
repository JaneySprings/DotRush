using System.Composition;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Embedded.Refactorings;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(WrapInTryCatchRefactoringProvider)), Shared]
public class WrapInTryCatchRefactoringProvider : CodeRefactoringProvider {
    public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context) {
        if (context.Span.IsEmpty)
            return;

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var sourceText = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        var selectedText = sourceText.ToString(context.Span);
        if (root == null || string.IsNullOrWhiteSpace(selectedText))
            return;

        var selection = TextSpan.FromBounds(
            context.Span.Start + selectedText.Length - selectedText.TrimStart().Length,
            context.Span.End - selectedText.Length + selectedText.TrimEnd().Length
        );
        var container = root.TryFindNode(selection)?.AncestorsAndSelf().FirstOrDefault(it => it is BlockSyntax || it is SwitchSectionSyntax);
        var statements = container is BlockSyntax block ? block.Statements : (container as SwitchSectionSyntax)?.Statements ?? default;
        var selectedStatements = statements.Where(it => it.Span.IntersectsWith(selection) && it.Span.End != selection.Start && it.SpanStart != selection.End).ToList();
        if (container == null || selectedStatements.Count == 0)
            return;
        if (selection.Start > selectedStatements[0].SpanStart || selection.End < selectedStatements[^1].Span.End)
            return;
        // CS1626: Cannot yield a value in the body of a try block with a catch clause
        if (selectedStatements.Any(it => it is LocalFunctionStatementSyntax || it.DescendantNodesAndSelf().Any(n => n.IsKind(SyntaxKind.YieldReturnStatement))))
            return;

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var dataFlow = semanticModel?.AnalyzeDataFlow(selectedStatements[0], selectedStatements[^1]);
        if (dataFlow == null || !dataFlow.Succeeded)
            return;
        if (dataFlow.VariablesDeclared.Any(it => dataFlow.ReadOutside.Contains(it) || dataFlow.WrittenOutside.Contains(it)))
            return;

        context.RegisterRefactoring(CodeAction.Create(
            "Wrap in try-catch",
            c => Task.FromResult(WrapInTryCatch(context.Document, container, statements, selectedStatements, root)),
            equivalenceKey: nameof(WrapInTryCatchRefactoringProvider)
        ));
    }
    private static Document WrapInTryCatch(Document document, SyntaxNode container, SyntaxList<StatementSyntax> statements, List<StatementSyntax> selectedStatements, SyntaxNode root) {
        var exceptionType = SyntaxFactory.ParseTypeName("System.Exception").WithAdditionalAnnotations(Simplifier.Annotation);
        var catchClause = SyntaxFactory.CatchClause()
            .WithDeclaration(SyntaxFactory.CatchDeclaration(exceptionType))
            .WithBlock(SyntaxFactory.Block(SyntaxFactory.ThrowStatement()));

        var tryBlock = SyntaxFactory.Block(selectedStatements.Select((it, index) => index == 0 ? it.WithoutLeadingTrivia() : it));
        var tryStatement = SyntaxFactory.TryStatement(tryBlock, SyntaxFactory.SingletonList(catchClause), null)
            .WithLeadingTrivia(selectedStatements[0].GetLeadingTrivia())
            .WithTrailingTrivia(selectedStatements[^1].GetTrailingTrivia().Where(it => it.IsKind(SyntaxKind.EndOfLineTrivia)))
            .WithAdditionalAnnotations(Formatter.Annotation);

        var startIndex = statements.IndexOf(selectedStatements[0]);
        var newStatements = statements.Take(startIndex).Append(tryStatement).Concat(statements.Skip(startIndex + selectedStatements.Count));
        var newContainer = container is BlockSyntax block
            ? (SyntaxNode)block.WithStatements(SyntaxFactory.List(newStatements))
            : ((SwitchSectionSyntax)container).WithStatements(SyntaxFactory.List(newStatements));

        return document.WithSyntaxRoot(root.ReplaceNode(container, newContainer));
    }
}