using System.Composition;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace DotRush.Roslyn.CodeAnalysis.Embedded.Refactorings;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(InitializeMembersRefactoringProvider)), Shared]
public class InitializeMembersRefactoringProvider : CodeRefactoringProvider {
    public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var objectCreation = root?.TryFindNode(context.Span)?.FirstAncestorOrSelf<BaseObjectCreationExpressionSyntax>();
        if (root == null || objectCreation == null)
            return;
        if (objectCreation.Initializer != null && !objectCreation.Initializer.IsKind(SyntaxKind.ObjectInitializerExpression))
            return;
        if (objectCreation.ArgumentList != null && context.Span.Start > objectCreation.ArgumentList.SpanStart && context.Span.End < objectCreation.ArgumentList.Span.End)
            return;

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel?.GetTypeInfo(objectCreation, context.CancellationToken).Type is not INamedTypeSymbol type || (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct))
            return;

        var position = objectCreation.SpanStart;
        var members = new List<ISymbol>();
        var memberNames = objectCreation.Initializer?.Expressions.OfType<AssignmentExpressionSyntax>().Select(it => it.Left.ToString()).ToHashSet() ?? new HashSet<string>();
        for (var currentType = type; currentType != null; currentType = currentType.BaseType) {
            var typeMembers = new List<ISymbol>();
            foreach (var member in currentType.GetMembers()) {
                if (member.IsStatic || member.IsImplicitlyDeclared)
                    continue;
                if (member is IFieldSymbol field && (field.IsReadOnly || field.IsConst || !semanticModel.IsAccessible(position, field)))
                    continue;
                if (member is IPropertySymbol property && (property.IsIndexer || property.SetMethod == null || !semanticModel.IsAccessible(position, property.SetMethod)))
                    continue;
                if ((member is IFieldSymbol || member is IPropertySymbol) && memberNames.Add(member.Name))
                    typeMembers.Add(member);
            }
            members.InsertRange(0, typeMembers);
        }
        if (members.Count == 0)
            return;

        var variables = semanticModel.LookupSymbols(position)
            .Where(it => it is ILocalSymbol || it is IParameterSymbol)
            .Where(it => it.DeclaringSyntaxReferences.All(r => r.Span.End <= position))
            .ToList();
        var assignments = members.ToDictionary(it => it, it => {
            var memberType = it is IFieldSymbol field ? field.Type : ((IPropertySymbol)it).Type;
            var variable = variables.FirstOrDefault(v => v.Name.Equals(it.Name, StringComparison.OrdinalIgnoreCase) && semanticModel.Compilation.ClassifyConversion(v.GetTypeSymbol()!, memberType).IsImplicit);
            return (ExpressionSyntax)SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName(it.Name),
                variable != null ? SyntaxFactory.IdentifierName(variable.Name) : SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression)
            );
        }, SymbolEqualityComparer.Default);

        var requiredMembers = members.Where(it => it is IFieldSymbol { IsRequired: true } || it is IPropertySymbol { IsRequired: true }).ToList();
        if (requiredMembers.Count > 0) {
            context.RegisterRefactoring(CodeAction.Create(
                "Initialize required members",
                c => Task.FromResult(InitializeMembers(context.Document, objectCreation, requiredMembers.Select(it => assignments[it]), root)),
                equivalenceKey: $"{nameof(InitializeMembersRefactoringProvider)}_Required"
            ));
        }
        if (requiredMembers.Count < members.Count) {
            context.RegisterRefactoring(CodeAction.Create(
                "Initialize all members",
                c => Task.FromResult(InitializeMembers(context.Document, objectCreation, members.Select(it => assignments[it]), root)),
                equivalenceKey: $"{nameof(InitializeMembersRefactoringProvider)}_All"
            ));
        }
    }
    private static Document InitializeMembers(Document document, BaseObjectCreationExpressionSyntax objectCreation, IEnumerable<ExpressionSyntax> assignments, SyntaxNode root) {
        var endOfLine = root.GetEndOfLine();
        var initializer = objectCreation.Initializer ?? SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression);
        var hasTrailingComma = initializer.Expressions.Count > 0 && initializer.Expressions.SeparatorCount == initializer.Expressions.Count;
        var expressions = initializer.Expressions.GetWithSeparators().ToList();

        if (expressions.Count > 0 && expressions[^1].IsNode) {
            var lastExpression = expressions[^1].AsNode()!;
            expressions[^1] = lastExpression.WithoutTrailingTrivia();
            expressions.Add(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(lastExpression.GetTrailingTrivia()));
        }
        if (expressions.Count > 0 && !expressions[^1].GetTrailingTrivia().Any(SyntaxKind.EndOfLineTrivia))
            expressions[^1] = expressions[^1].AsToken().WithTrailingTrivia(endOfLine);

        foreach (var assignment in assignments) {
            expressions.Add(assignment.WithAdditionalAnnotations(Formatter.Annotation));
            expressions.Add(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(endOfLine));
        }
        if (!hasTrailingComma) {
            expressions.RemoveAt(expressions.Count - 1);
            expressions[^1] = expressions[^1].AsNode()!.WithTrailingTrivia(endOfLine);
        }

        var openBraceToken = initializer.OpenBraceToken;
        if (!openBraceToken.TrailingTrivia.Any(SyntaxKind.EndOfLineTrivia)) {
            openBraceToken = openBraceToken.WithTrailingTrivia(endOfLine);
            expressions[0] = expressions[0].AsNode()!.WithAdditionalAnnotations(Formatter.Annotation);
        }

        var newInitializer = initializer
            .WithOpenBraceToken(openBraceToken)
            .WithExpressions(SyntaxFactory.SeparatedList<ExpressionSyntax>(expressions))
            .WithCloseBraceToken(initializer.CloseBraceToken.WithAdditionalAnnotations(Formatter.Annotation));

        return document.WithSyntaxRoot(root.ReplaceNode(objectCreation, objectCreation.WithInitializer(newInitializer)));
    }
}