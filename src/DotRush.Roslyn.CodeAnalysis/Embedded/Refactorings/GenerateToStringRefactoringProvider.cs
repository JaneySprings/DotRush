using System.Composition;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace DotRush.Roslyn.CodeAnalysis.Embedded.Refactorings;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(GenerateToStringRefactoringProvider)), Shared]
public class GenerateToStringRefactoringProvider : CodeRefactoringProvider {
    public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root?.TryFindNode(context.Span) is not TypeDeclarationSyntax typeDeclaration || !typeDeclaration.Identifier.Span.IntersectsWith(context.Span))
            return;
        if (typeDeclaration is not ClassDeclarationSyntax && typeDeclaration is not StructDeclarationSyntax)
            return;

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var typeSymbol = semanticModel?.GetDeclaredSymbol(typeDeclaration, context.CancellationToken);
        if (typeSymbol == null || typeSymbol.IsStatic || typeSymbol.GetMembers(nameof(ToString)).Any(it => it is IMethodSymbol { Parameters.Length: 0 }))
            return;

        var members = typeSymbol.GetMembers()
            .Where(it => it.DeclaredAccessibility == Accessibility.Public && !it.IsStatic && !it.IsImplicitlyDeclared)
            .Where(it => it is IFieldSymbol || it is IPropertySymbol { IsIndexer: false, GetMethod: not null })
            .Select(it => $"{it.Name} = {{{it.Name}}}")
            .ToList();
        if (members.Count == 0)
            return;

        context.RegisterRefactoring(CodeAction.Create(
            "Generate ToString()",
            c => Task.FromResult(GenerateToString(context.Document, typeDeclaration, members, root)),
            equivalenceKey: nameof(GenerateToStringRefactoringProvider)
        ));
    }
    private static Document GenerateToString(Document document, TypeDeclarationSyntax typeDeclaration, List<string> members, SyntaxNode root) {
        var typeName = $"{typeDeclaration.Identifier.Text}{typeDeclaration.TypeParameterList?.WithoutTrivia()}";
        var method = SyntaxFactory.ParseMemberDeclaration($"public override string ToString() => $\"{{nameof({typeName})}} {{{{ {string.Join(", ", members)} }}}}\";")!
            .WithLeadingTrivia(root.GetEndOfLine())
            .WithTrailingTrivia(root.GetEndOfLine())
            .WithAdditionalAnnotations(Formatter.Annotation);

        return document.WithSyntaxRoot(root.ReplaceNode(typeDeclaration, typeDeclaration.AddMembers(method)));
    }
}