/*
 * This code was generated using Claude 3.7 Sonnet (Preview)
 * via Visual Studio Code
 */

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Embedded.Refactorings;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(ExtractToNewMethodRefactoringProvider)), Shared]
public class ExtractToNewMethodRefactoringProvider : CodeRefactoringProvider {
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context) {
        var document = context.Document;
        var span = context.Span;
        var cancellationToken = context.CancellationToken;

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return;

        var methodNode = root.FindNode(span).FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodNode == null || methodNode.Body == null)
            return;

        var newMethodName = $"{methodNode.Identifier.Text}Part";
        var action = CodeAction.Create($"Extract to new method '{newMethodName}'", _ => Task.FromResult(ExtractMethod(document, root, methodNode, span, newMethodName)));
        context.RegisterRefactoring(action);
    }

    private Document ExtractMethod(Document document, SyntaxNode root, MethodDeclarationSyntax methodNode, TextSpan span, string newMethodName) {
        if (methodNode.Body == null)
            return document;

        // Get the selected statements
        var toExtractStatements = methodNode.Body.Statements.Where(s => s.Span.IntersectsWith(span)).ToList();
        if (toExtractStatements.Count == 0)
            return document;

        // Check if any of the extracted statements contain a return statement
        var containsReturn = toExtractStatements.Any(s => s is ReturnStatementSyntax || s.DescendantNodes().OfType<ReturnStatementSyntax>().Any());

        // Determine the return type for the new method
        TypeSyntax returnType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
        if (containsReturn && methodNode.ReturnType != null)
            returnType = methodNode.ReturnType;

        // Find used parameters in the selected statements
        var usedParameterIdentifiers = new HashSet<string>();
        foreach (var statement in toExtractStatements) {
            var identifiers = statement.DescendantNodes().OfType<IdentifierNameSyntax>();
            foreach (var identifier in identifiers)
                usedParameterIdentifiers.Add(identifier.Identifier.Text);
        }

        // Filter original method parameters that are used in the extracted code
        var parameterList = new List<ParameterSyntax>();
        var argumentList = new List<ArgumentSyntax>();
        if (methodNode.ParameterList != null) {
            foreach (var param in methodNode.ParameterList.Parameters) {
                var paramName = param.Identifier.Text;
                if (usedParameterIdentifiers.Contains(paramName)) {
                    parameterList.Add(param);
                    argumentList.Add(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(paramName)));
                }
            }
        }

        // Create the new method with parameters
        var extractedMethod = SyntaxFactory.MethodDeclaration(
            returnType,
            SyntaxFactory.Identifier(newMethodName))
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameterList)))
            .WithBody(SyntaxFactory.Block(toExtractStatements));

        // Create the extracted method invocation with arguments
        StatementSyntax extractedMethodStatement;
        var invocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.IdentifierName(newMethodName),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(argumentList)));

        if (containsReturn) {
            extractedMethodStatement = SyntaxFactory.ReturnStatement(invocation);
        } else {
            extractedMethodStatement = SyntaxFactory.ExpressionStatement(invocation);
        }

        // Replace the selected statements with a call to the new method
        var currentMethodBodyStatements = new List<StatementSyntax>();
        foreach (var statement in methodNode.Body.Statements) {
            if (!toExtractStatements.Contains(statement)) {
                currentMethodBodyStatements.Add(statement);
                continue;
            }
            if (!currentMethodBodyStatements.Contains(extractedMethodStatement))
                currentMethodBodyStatements.Add(extractedMethodStatement);
        }
        var currentMethodWithUpdates = methodNode.WithBody(SyntaxFactory.Block(currentMethodBodyStatements));

        // Replace old method with new method and extracted method
        var newRoot = root.ReplaceNode(methodNode, new[] { currentMethodWithUpdates, extractedMethod });
        return document.WithSyntaxRoot(newRoot);
    }
}