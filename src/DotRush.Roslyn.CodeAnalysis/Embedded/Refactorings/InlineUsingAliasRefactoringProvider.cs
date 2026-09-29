using System.Composition;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Embedded.Refactorings;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(InlineUsingAliasRefactoringProvider)), Shared]
public class InlineUsingAliasRefactoringProvider : CodeRefactoringProvider {
    private static readonly SymbolDisplayFormat targetDisplayFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var usingDirective = root?.TryFindNode(context.Span)?.FirstAncestorOrSelf<UsingDirectiveSyntax>();

        if (root == null || usingDirective?.Alias == null)
            return;

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel?.GetDeclaredSymbol(usingDirective, context.CancellationToken) is not IAliasSymbol aliasSymbol || aliasSymbol.Target is IErrorTypeSymbol)
            return;

        context.RegisterRefactoring(CodeAction.Create(
            "Inline using alias",
            c => InlineAliasAsync(context.Document, semanticModel, usingDirective, aliasSymbol, c),
            equivalenceKey: nameof(InlineUsingAliasRefactoringProvider)
        ));
    }
    private async Task<Document> InlineAliasAsync(Document document, SemanticModel semanticModel, UsingDirectiveSyntax usingDirective, IAliasSymbol aliasSymbol, CancellationToken cancellationToken) {
        var root = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var targetName = aliasSymbol.Target.ToDisplayString(targetDisplayFormat);
        var textChanges = new List<TextChange>();

        // Global alias can be used in other documents
        if (!usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)) {
            var line = sourceText.Lines.GetLineFromPosition(usingDirective.SpanStart);
            var nextLine = sourceText.Lines.GetLineFromPosition(usingDirective.FullSpan.End);
            var removalSpan = usingDirective.Span;

            if (string.IsNullOrWhiteSpace(sourceText.ToString(TextSpan.FromBounds(line.Start, usingDirective.SpanStart)))) {
                var isSeparated = line.LineNumber == 0 || string.IsNullOrWhiteSpace(sourceText.Lines[line.LineNumber - 1].ToString());
                removalSpan = TextSpan.FromBounds(line.Start, isSeparated && string.IsNullOrWhiteSpace(nextLine.ToString()) ? nextLine.EndIncludingLineBreak : usingDirective.FullSpan.End);
            }
            textChanges.Add(new TextChange(removalSpan, string.Empty));
        }

        foreach (var identifierName in root.DescendantNodes(descendIntoTrivia: true).OfType<IdentifierNameSyntax>()) {
            var name = identifierName.Identifier.ValueText;
            if (name != aliasSymbol.Name && name + "Attribute" != aliasSymbol.Name)
                continue;
            if (usingDirective.Span.Contains(identifierName.Span))
                continue;
            if (!SymbolEqualityComparer.Default.Equals(aliasSymbol, semanticModel.GetAliasInfo(identifierName, cancellationToken)))
                continue;

            var newText = identifierName.FirstAncestorOrSelf<CrefSyntax>() == null ? targetName : targetName.Replace('<', '{').Replace('>', '}');
            if (identifierName.Parent is AliasQualifiedNameSyntax aliasQualifiedName && aliasQualifiedName.Alias == identifierName)
                textChanges.Add(new TextChange(TextSpan.FromBounds(identifierName.SpanStart, aliasQualifiedName.ColonColonToken.Span.End), newText + "."));
            else
                textChanges.Add(new TextChange(identifierName.Span, newText));
        }

        return document.WithText(sourceText.WithChanges(textChanges));
    }
}