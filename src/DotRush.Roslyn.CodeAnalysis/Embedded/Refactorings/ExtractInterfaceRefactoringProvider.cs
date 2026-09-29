using System.Composition;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace DotRush.Roslyn.CodeAnalysis.Embedded.Refactorings;

// Replacement for the ExtractInterfaceCodeRefactoringProvider that requires an options dialog
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(ExtractInterfaceRefactoringProvider)), Shared]
public class ExtractInterfaceRefactoringProvider : CodeRefactoringProvider {
    public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root?.TryFindNode(context.Span) is not TypeDeclarationSyntax typeDeclaration || typeDeclaration is InterfaceDeclarationSyntax)
            return;
        if (!typeDeclaration.Identifier.Span.IntersectsWith(context.Span))
            return;

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var typeSymbol = semanticModel?.GetDeclaredSymbol(typeDeclaration, context.CancellationToken);
        if (semanticModel == null || typeSymbol == null || typeSymbol.IsStatic)
            return;

        var interfaceName = $"I{typeSymbol.Name}";
        if (semanticModel.LookupNamespacesAndTypes(typeDeclaration.SpanStart, name: interfaceName).Length > 0)
            return;

        var members = new List<MemberDeclarationSyntax>();
        var implementedMembers = typeSymbol.AllInterfaces.SelectMany(it => it.GetMembers()).Select(typeSymbol.FindImplementationForInterfaceMember).ToHashSet(SymbolEqualityComparer.Default);
        foreach (var member in typeSymbol.GetMembers()) {
            if (member.DeclaredAccessibility != Accessibility.Public || member.IsStatic || member.IsOverride || implementedMembers.Contains(member))
                continue;

            var syntax = member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken);
            var interfaceMember = CreateInterfaceMember(member, syntax);
            if (syntax == null || interfaceMember == null)
                continue;

            var documentation = syntax.GetLeadingTrivia().Where(it => it.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || it.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
            members.Add(interfaceMember
                .WithAttributeLists(default)
                .WithModifiers(default)
                .WithLeadingTrivia(documentation)
                .WithTrailingTrivia(root.GetEndOfLine()));
        }
        if (members.Count == 0)
            return;

        context.RegisterRefactoring(CodeAction.Create(
            $"Extract interface '{interfaceName}'",
            c => Task.FromResult(ExtractInterface(context.Document, typeDeclaration, interfaceName, members, root)),
            equivalenceKey: nameof(ExtractInterfaceRefactoringProvider)
        ));
    }
    private static Document ExtractInterface(Document document, TypeDeclarationSyntax typeDeclaration, string interfaceName, List<MemberDeclarationSyntax> members, SyntaxNode root) {
        var leadingTrivia = typeDeclaration.GetLeadingTrivia();
        var interfaceDeclaration = SyntaxFactory.InterfaceDeclaration(interfaceName)
            .WithModifiers(SyntaxFactory.TokenList(typeDeclaration.Modifiers.Where(it => SyntaxFacts.IsAccessibilityModifier(it.Kind())).Select(it => it.WithoutTrivia().WithTrailingTrivia(SyntaxFactory.ElasticSpace))))
            .WithTypeParameterList(typeDeclaration.TypeParameterList?.WithoutTrivia())
            .WithConstraintClauses(SyntaxFactory.List(typeDeclaration.ConstraintClauses.Select(it => it.WithoutTrivia().WithLeadingTrivia(SyntaxFactory.ElasticSpace))))
            .WithMembers(SyntaxFactory.List(members))
            .WithLeadingTrivia(leadingTrivia.TakeWhile(it => it.IsKind(SyntaxKind.WhitespaceTrivia) || it.IsKind(SyntaxKind.EndOfLineTrivia)))
            .WithTrailingTrivia(root.GetEndOfLine())
            .WithAdditionalAnnotations(Formatter.Annotation);

        var typeArguments = typeDeclaration.TypeParameterList == null ? string.Empty : $"<{string.Join(", ", typeDeclaration.TypeParameterList.Parameters.Select(it => it.Identifier.Text))}>";
        var headerToken = (typeDeclaration.BaseList ?? typeDeclaration.ParameterList ?? typeDeclaration.TypeParameterList as SyntaxNode)?.GetLastToken() ?? typeDeclaration.Identifier;
        var baseType = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(interfaceName + typeArguments)).WithTrailingTrivia(headerToken.TrailingTrivia);
        var newTypeDeclaration = typeDeclaration
            .ReplaceToken(headerToken, headerToken.WithTrailingTrivia())
            .AddBaseListTypes(baseType);
        if (!leadingTrivia.Any(SyntaxKind.EndOfLineTrivia))
            newTypeDeclaration = newTypeDeclaration.WithLeadingTrivia(leadingTrivia.Insert(0, root.GetEndOfLine()));

        return document.WithSyntaxRoot(root.ReplaceNode(typeDeclaration, new SyntaxNode[] { interfaceDeclaration, newTypeDeclaration }));
    }

    private static MemberDeclarationSyntax? CreateInterfaceMember(ISymbol member, SyntaxNode? syntax) {
        var semicolonToken = SyntaxFactory.Token(SyntaxKind.SemicolonToken);
        if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary } && syntax is MethodDeclarationSyntax method)
            return method.WithBody(null).WithExpressionBody(null).WithSemicolonToken(semicolonToken);
        if (member is IEventSymbol && syntax is EventDeclarationSyntax eventDeclaration)
            return SyntaxFactory.EventFieldDeclaration(SyntaxFactory.VariableDeclaration(eventDeclaration.Type, SyntaxFactory.SingletonSeparatedList(SyntaxFactory.VariableDeclarator(eventDeclaration.Identifier.WithoutTrivia()))));
        if (member is IEventSymbol && syntax is VariableDeclaratorSyntax { Parent.Parent: EventFieldDeclarationSyntax eventField } variable)
            return eventField.WithDeclaration(eventField.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(variable.WithInitializer(null))));
        if (member is not IPropertySymbol property)
            return null;

        var accessors = new List<AccessorDeclarationSyntax>();
        if (property.GetMethod?.DeclaredAccessibility == Accessibility.Public)
            accessors.Add(SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(semicolonToken));
        if (property.SetMethod?.DeclaredAccessibility == Accessibility.Public)
            accessors.Add(SyntaxFactory.AccessorDeclaration(property.SetMethod.IsInitOnly ? SyntaxKind.InitAccessorDeclaration : SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(semicolonToken));

        var accessorList = SyntaxFactory.AccessorList(SyntaxFactory.List(accessors));
        return syntax switch {
            PropertyDeclarationSyntax propertyDeclaration => propertyDeclaration.WithAccessorList(accessorList).WithExpressionBody(null).WithInitializer(null).WithSemicolonToken(default),
            IndexerDeclarationSyntax indexerDeclaration => indexerDeclaration.WithAccessorList(accessorList).WithExpressionBody(null).WithSemicolonToken(default),
            ParameterSyntax { Type: not null } parameter => SyntaxFactory.PropertyDeclaration(parameter.Type.WithoutTrivia(), parameter.Identifier.WithoutTrivia()).WithAccessorList(accessorList),
            _ => null
        };
    }
}