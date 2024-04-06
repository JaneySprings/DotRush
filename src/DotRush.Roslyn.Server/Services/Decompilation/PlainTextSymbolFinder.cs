using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.Server.Services;

public class PlainTextSymbolFinder : CSharpSyntaxWalker {
    public List<TextSpan> Locations { get; private set;}
    private readonly ISymbol targetSymbol;

    public PlainTextSymbolFinder(ISymbol targetSymbol) {
        Locations = new List<TextSpan>();
        this.targetSymbol = targetSymbol;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node) {
        if (node.Identifier.Text == targetSymbol.Name)
            Locations.Add(node.Identifier.Span);
        base.VisitClassDeclaration(node);
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) {
        if (node.Identifier.Text == targetSymbol.Name)
            Locations.Add(node.Identifier.Span);
        base.VisitInterfaceDeclaration(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node) {
        if (node.Identifier.Text == targetSymbol.Name)
            Locations.Add(node.Identifier.Span);
        base.VisitMethodDeclaration(node);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node) {
        if (node.Identifier.Text == targetSymbol.Name)
            Locations.Add(node.Identifier.Span);
        base.VisitPropertyDeclaration(node);
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node) {
        foreach (var variable in node.Declaration.Variables) {
            if (variable.Identifier.Text == targetSymbol.Name)
                Locations.Add(variable.Span);
        }
        base.VisitFieldDeclaration(node);
    }

    public override void VisitEventDeclaration(EventDeclarationSyntax node) {
        if (node.Identifier.Text == targetSymbol.Name)
            Locations.Add(node.Identifier.Span);
        base.VisitEventDeclaration(node);
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node) {
        if (node.Identifier.Text == targetSymbol.Name)
            Locations.Add(node.Identifier.Span);
        base.VisitConstructorDeclaration(node);
    }

    public override void VisitDestructorDeclaration(DestructorDeclarationSyntax node) {
        if (node.Identifier.Text == targetSymbol.Name)
            Locations.Add(node.Identifier.Span);
        base.VisitDestructorDeclaration(node);
    }
}