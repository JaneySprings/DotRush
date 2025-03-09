using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Tests;

public abstract class RefactoringsTestFixture : WorkspaceTestFixture {
    protected const string SingleTFM = "net8.0";
    protected const string TestProjectName = "TestProjectSingle";
    protected const string TestOutputType = "Library";

    protected override string CreateProject(string name, string tfm, string outputType) {
        return base.CreateProject(TestProjectName, SingleTFM, TestOutputType);
    }

    protected Document CreateDocument(string name, string source) {
        var documentPath = CreateFileInProject(name, source);
        Workspace!.CreateDocument(documentPath);
        return Workspace.Solution!.GetDocument(Workspace.Solution.GetDocumentIdsWithFilePathV2(documentPath).Single())!;
    }
    protected async Task<List<CodeAction>> GetRefactoringActionsAsync(CodeRefactoringProvider provider, Document document, TextSpan span) {
        var actions = new List<CodeAction>();
        await provider.ComputeRefactoringsAsync(new CodeRefactoringContext(
            document, span,
            a => actions.Add(a),
            default));

        return actions;
    }
    protected async Task<Document> ApplyRefactoringAsync(CodeAction action, Document document) {
        var operations = await action.GetOperationsAsync(default);
        var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        return solution.GetDocument(document.Id)!;
    }
}