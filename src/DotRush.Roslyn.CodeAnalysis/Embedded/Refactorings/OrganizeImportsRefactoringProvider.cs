// https://github.com/dotnet/roslyn/blob/d0ecb5038a387c3596e4c9bb3b17b1032b1c011c/src/Workspaces/CSharp/Portable/OrganizeImports/CSharpOrganizeImportsService.cs#L15
// https://github.com/dotnet/roslyn/blob/main/src/Workspaces/SharedUtilitiesAndExtensions/Compiler/Core/OrganizeImports/OrganizeImportsOptions.cs#L18

using System.Composition;
using DotRush.Roslyn.CodeAnalysis.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotRush.Roslyn.CodeAnalysis.Embedded.Refactorings;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(OrganizeImportsRefactoringProvider)), Shared]
public class OrganizeImportsRefactoringProvider : CodeRefactoringProvider {
    private object? organizeImportsService = InternalCSharpOrganizeImportsService.CreateNew();

    public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var node = root?.FindNode(context.Span);

        if (node is not CompilationUnitSyntax) {
            var usingDerictive = node?.FirstAncestorOrSelf<UsingDirectiveSyntax>();
            if (usingDerictive?.Parent is not CompilationUnitSyntax)
                return;
            if (usingDerictive.Parent is CompilationUnitSyntax compilationUnit && !compilationUnit.Usings.Any())
                return;
        }

        context.RegisterRefactoring(CodeAction.Create(
            "Sort usings",
            c => SortUsingsAsync(context.Document, false, false, c),
            equivalenceKey: "Sort usings alphabetically"
        ));
        context.RegisterRefactoring(CodeAction.Create(
            "Sort usings and place 'System' directives first",
            c => SortUsingsAsync(context.Document, true, false, c),
            equivalenceKey: "Sort usings system first"
        ));
        context.RegisterRefactoring(CodeAction.Create(
            "Sort usings and separate groups",
            c => SortUsingsAsync(context.Document, false, true, c),
            equivalenceKey: "Sort usings separate groups"
        ));
    }

    private Task<Document> SortUsingsAsync(Document document, bool placeSystemFirst, bool separateGroups, CancellationToken cancellationToken) {
        var organizeImportsOptions = InternalOrganizeImportsOptions.CreateNew();
        if (organizeImportsOptions == null || organizeImportsService == null)
            return Task.FromResult(document);

        InternalOrganizeImportsOptions.AssignValues(organizeImportsOptions, placeSystemFirst, separateGroups);
        
        var newDocumentTask = InternalCSharpOrganizeImportsService.OrganizeImportsAsync(organizeImportsService, document, organizeImportsOptions, cancellationToken);
        if (newDocumentTask == null)
            return Task.FromResult(document);

        return newDocumentTask;
    }
}