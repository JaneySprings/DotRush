using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

        return kind >= SyntaxKind.IfKeyword && kind <= SyntaxKind.ThrowKeyword || kind == SyntaxKind.UsingKeyword;
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
}