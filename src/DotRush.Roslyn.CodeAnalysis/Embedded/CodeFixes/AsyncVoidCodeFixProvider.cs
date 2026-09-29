using System.Collections.Immutable;
using System.Composition;
using DotRush.Roslyn.CodeAnalysis.Embedded.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;

namespace DotRush.Roslyn.CodeAnalysis.Embedded.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncVoidCodeFixProvider)), Shared]
public class AsyncVoidCodeFixProvider : CodeFixProvider {
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(AsyncVoidDiagnosticAnalyzer.DiagnosticId);

    public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        foreach (var diagnostic in context.Diagnostics) {
            var returnType = root?.FindNode(diagnostic.Location.SourceSpan) switch {
                MethodDeclarationSyntax method => method.ReturnType,
                LocalFunctionStatementSyntax localFunction => localFunction.ReturnType,
                _ => null
            };
            if (root == null || returnType == null)
                continue;

            context.RegisterCodeFix(CodeAction.Create(
                "Change return type to 'Task'",
                c => Task.FromResult(ChangeReturnType(context.Document, returnType, root)),
                equivalenceKey: nameof(AsyncVoidCodeFixProvider)
            ), diagnostic);
        }
    }
    public sealed override FixAllProvider? GetFixAllProvider() {
        return WellKnownFixAllProviders.BatchFixer;
    }

    private static Document ChangeReturnType(Document document, TypeSyntax returnType, SyntaxNode root) {
        var taskType = SyntaxFactory.ParseTypeName("System.Threading.Tasks.Task")
            .WithTriviaFrom(returnType)
            .WithAdditionalAnnotations(Simplifier.Annotation);

        return document.WithSyntaxRoot(root.ReplaceNode(returnType, taskType));
    }
}