using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotRush.Roslyn.CodeAnalysis.Embedded.Refactorings;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(GenerateDescriptionRefactoringProvider)), Shared]
public class GenerateDescriptionRefactoringProvider : CodeRefactoringProvider {
    public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var node = root?.FindNode(context.Span);

        if (root == null || node is not MemberDeclarationSyntax memberDeclaration)
            return;

        context.RegisterRefactoring(CodeAction.Create(
            "Generate description in XML",
            c => Task.FromResult(GenerateDocumentation(context.Document, memberDeclaration, root)),
            equivalenceKey: nameof(GenerateDescriptionRefactoringProvider)
        ));
    }
    private Document GenerateDocumentation(Document document, MemberDeclarationSyntax memberDeclaration, SyntaxNode root) {
        var xmlNodes = new List<XmlNodeSyntax>();
        AddSummaryDocumentation(xmlNodes);

        switch (memberDeclaration) {
            case BaseMethodDeclarationSyntax baseMethodDeclaration:
                AddMethodDocumentation(baseMethodDeclaration, xmlNodes);
                break;
            case BasePropertyDeclarationSyntax basePropertyDeclaration:
                AddPropertyDocumentation(basePropertyDeclaration, xmlNodes);
                break;
        }

        var documentationComment = SyntaxFactory.DocumentationCommentTrivia(
            SyntaxKind.SingleLineDocumentationCommentTrivia,
            new SyntaxList<XmlNodeSyntax>(xmlNodes)
        );
        var newMemberDeclaration = memberDeclaration.WithLeadingTrivia(
            memberDeclaration.GetLeadingTrivia()
                .Add(SyntaxFactory.Trivia(documentationComment))
                .Add(SyntaxFactory.ElasticCarriageReturnLineFeed)
        );

        var newRoot = root.ReplaceNode(memberDeclaration, newMemberDeclaration);
        return document.WithSyntaxRoot(newRoot);
    }

    private void AddSummaryDocumentation(List<XmlNodeSyntax> xmlNodes) {
        xmlNodes.Add(CreateFirstNewLine());
        xmlNodes.Add(SyntaxFactory.XmlSummaryElement(
            CreateNewLine(),
            SyntaxFactory.XmlText(SyntaxFactory.XmlTextLiteral(string.Empty)),
            CreateNewLine()
        ));
    }
    private void AddMethodDocumentation(BaseMethodDeclarationSyntax baseMethodDeclaration, List<XmlNodeSyntax> xmlNodes) {
        AddParametersDocumentation(baseMethodDeclaration.ParameterList.Parameters, xmlNodes);

        if (baseMethodDeclaration is MethodDeclarationSyntax methodDeclaration) {
            if (methodDeclaration.ReturnType is not PredefinedTypeSyntax predefinedType || !predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword)) {
                xmlNodes.Add(CreateNewLine());
                xmlNodes.Add(SyntaxFactory.XmlReturnsElement(
                    SyntaxFactory.XmlText(SyntaxFactory.XmlTextLiteral(string.Empty))
                ));
            }
        }

        AddExceptionDocumentation(baseMethodDeclaration, xmlNodes);
    }
    private void AddPropertyDocumentation(BasePropertyDeclarationSyntax basePropertyDeclaration, List<XmlNodeSyntax> xmlNodes) {
        if (basePropertyDeclaration is IndexerDeclarationSyntax indexerDeclaration)
            AddParametersDocumentation(indexerDeclaration.ParameterList.Parameters, xmlNodes);

        AddExceptionDocumentation(basePropertyDeclaration, xmlNodes);
    }
    private void AddParametersDocumentation(SeparatedSyntaxList<ParameterSyntax> parameters, List<XmlNodeSyntax> xmlNodes) {
        foreach (var parameter in parameters) {
            var paramElement = SyntaxFactory.XmlParamElement(
                parameter.Identifier.Text,
                SyntaxFactory.XmlText(SyntaxFactory.XmlTextLiteral(string.Empty))
            );
            xmlNodes.Add(CreateNewLine());
            xmlNodes.Add(paramElement);
        }
    }
    private void AddExceptionDocumentation(CSharpSyntaxNode syntaxNode, List<XmlNodeSyntax> xmlNodes) {
        var throwSyntaxes = syntaxNode.DescendantNodes().OfType<ThrowStatementSyntax>();
        foreach (var throwSyntax in throwSyntaxes) {
            var exceptionType = throwSyntax.Expression?.DescendantNodes().OfType<TypeSyntax>().FirstOrDefault();
            if (exceptionType != null) {
                xmlNodes.Add(CreateNewLine());
                xmlNodes.Add(SyntaxFactory.XmlExceptionElement(
                    SyntaxFactory.TypeCref(exceptionType),
                    SyntaxFactory.XmlText(SyntaxFactory.XmlTextLiteral(string.Empty))
                ));
            }
        }
    }

    private static XmlTextSyntax CreateFirstNewLine() {
        return SyntaxFactory.XmlText("/// ");
    }
    private static XmlTextSyntax CreateNewLine() {
        return SyntaxFactory.XmlText().WithTextTokens(
            SyntaxFactory.TokenList(
                SyntaxFactory.XmlTextNewLine(Environment.NewLine, continueXmlDocumentationComment: true)
            )
        );
    }
}