using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Extensions;

public static class SyntaxExtensions {
    public static bool IsControlKeyword(this SyntaxToken token) {
        var kind = token.Kind();

        // 'default' used in 'default' switch-case expressions
        if (token.IsKind(SyntaxKind.DefaultKeyword) && token.Parent is not DefaultSwitchLabelSyntax)
            return false;
        // 'using' used in 'using var x' expressions
        if (token.IsKind(SyntaxKind.UsingKeyword) && token.Parent is UsingDirectiveSyntax)
            return false;

        return kind >= SyntaxKind.IfKeyword && kind <= SyntaxKind.ThrowKeyword
            || kind == SyntaxKind.UsingKeyword
            || kind == SyntaxKind.YieldKeyword;
    }
    public static bool IsRegularKeyword(this SyntaxToken token) {
        if (token.IsControlKeyword())
            return false;

        // For 'var name' declarations, 'var' is a keyword
        if (token.Parent is IdentifierNameSyntax declaration && declaration.IsVar)
            return true;

        return token.IsKeyword();
    }
    public static bool IsStringExpression(this SyntaxToken token) {
        if (token.IsKind(SyntaxKind.StringLiteralToken) || token.IsKind(SyntaxKind.CharacterLiteralToken))
            return true;

        if (token.IsKind(SyntaxKind.InterpolatedStringStartToken) || token.IsKind(SyntaxKind.InterpolatedStringTextToken) || token.IsKind(SyntaxKind.InterpolatedStringEndToken))
            return true;

        return false;
    }
    // public static bool IsOperator(this SyntaxToken token) {
    // }
    public static bool IsDeclaration(this SyntaxNode? node) {
        return node is MemberDeclarationSyntax ||
               node is ParameterSyntax ||
               node is VariableDeclaratorSyntax;
    }
    public static string ToDisplayString(this SyntaxNode node) {
        var name = node switch {
            BaseNamespaceDeclarationSyntax namespaceDeclaration => namespaceDeclaration.Name.ToString(),
            ExtensionBlockDeclarationSyntax => "extension",
            BaseTypeDeclarationSyntax typeDeclaration => typeDeclaration.Identifier.Text,
            DelegateDeclarationSyntax delegateDeclaration => delegateDeclaration.Identifier.Text,
            ConstructorDeclarationSyntax constructorDeclaration => constructorDeclaration.Identifier.Text,
            DestructorDeclarationSyntax destructorDeclaration => $"~{destructorDeclaration.Identifier.Text}",
            MethodDeclarationSyntax methodDeclaration => methodDeclaration.Identifier.Text,
            OperatorDeclarationSyntax operatorDeclaration => $"operator {operatorDeclaration.OperatorToken.Text}",
            ConversionOperatorDeclarationSyntax conversionDeclaration => $"{conversionDeclaration.ImplicitOrExplicitKeyword.Text} operator {conversionDeclaration.Type}",
            PropertyDeclarationSyntax propertyDeclaration => propertyDeclaration.Identifier.Text,
            IndexerDeclarationSyntax => "this[]",
            EventDeclarationSyntax eventDeclaration => eventDeclaration.Identifier.Text,
            EnumMemberDeclarationSyntax enumMemberDeclaration => enumMemberDeclaration.Identifier.Text,
            VariableDeclaratorSyntax variableDeclarator => variableDeclarator.Identifier.Text,
            _ => null
        };
        var typeParameterList = node switch {
            TypeDeclarationSyntax typeDeclaration => typeDeclaration.TypeParameterList,
            DelegateDeclarationSyntax delegateDeclaration => delegateDeclaration.TypeParameterList,
            MethodDeclarationSyntax methodDeclaration => methodDeclaration.TypeParameterList,
            _ => null
        };
        var parameterList = node switch {
            BaseMethodDeclarationSyntax methodDeclaration => methodDeclaration.ParameterList,
            ExtensionBlockDeclarationSyntax extensionDeclaration => extensionDeclaration.ParameterList,
            _ => null
        };

        if (string.IsNullOrEmpty(name))
            name = "?";
        if (typeParameterList != null)
            name += $"<{string.Join(", ", typeParameterList.Parameters.Select(p => p.Identifier.Text))}>";
        if (parameterList != null)
            name += $"({string.Join(", ", parameterList.Parameters.Select(p => p.Type?.ToString() ?? "?"))})";

        return name;
    }

    public static SyntaxTrivia GetEndOfLine(this SyntaxNode node) {
        var endOfLine = node.DescendantTrivia().FirstOrDefault(it => it.IsKind(SyntaxKind.EndOfLineTrivia));
        return endOfLine.IsKind(SyntaxKind.None) ? SyntaxFactory.EndOfLine(Environment.NewLine) : SyntaxFactory.EndOfLine(endOfLine.ToString());
    }

    public static SyntaxNode? TryFindNode(this SyntaxNode node, TextSpan span) {
        try {
            return node.FindNode(span);
        } catch {
            return null;
        }
    }
}