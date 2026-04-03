using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.CodeAction;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class CodeActionHandlerFixAllTests : SimpleWorkspaceFixture {
    private CodeAnalysisService codeAnalysisService = null!;
    private CodeActionHandlerMock handler = null!;
    private string primaryDocumentPath = null!;
    private string siblingDocumentPath = null!;
    private string otherProjectDocumentPath = null!;

    [SetUp]
    public async Task LoadWorkspaceAsync() {
        codeAnalysisService = new CodeAnalysisService(new ConfigurationService(null), null);
        handler = new CodeActionHandlerMock(Workspace, codeAnalysisService);

        var projectOne = CreateProject("ProjectOne");
        var projectTwo = CreateProject("ProjectTwo");
        primaryDocumentPath = CreateSourceFile(projectOne, "PrimaryDocument");
        siblingDocumentPath = CreateSourceFile(projectOne, "SiblingDocument");
        otherProjectDocumentPath = CreateSourceFile(projectTwo, "OtherProjectDocument");

        await Workspace.LoadAsync(new[] { projectOne, projectTwo }, CancellationToken.None).ConfigureAwait(false);
    }

    [Test]
    public async Task GetFixAllCodeActionsForUnnecessaryUsingsTest() {
        var documents = Workspace.Solution!.Projects.SelectMany(project => project.Documents).ToArray();
        await codeAnalysisService.AnalyzeAsync(documents, AnalysisScope.Solution, AnalysisScope.None, CancellationToken.None).ConfigureAwait(false);

        var document = Workspace.Solution.GetDocumentIdsWithFilePath(primaryDocumentPath)
            .Select(id => Workspace.Solution.GetDocument(id))
            .Single()!;

        var result = await handler.Handle(new CodeActionParams {
            TextDocument = document.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(0, 0, 1, 0)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result.CommandOrCodeActions, Is.Not.Null.Or.Empty);
        Assert.That(result.CommandOrCodeActions, Has.One.Matches<CommandOrCodeAction>(action => action.CodeAction?.Title == "Remove unnecessary usings"));

        var fixAllActions = result.CommandOrCodeActions
            .Select(action => action.CodeAction)
            .OfType<CodeAction>()
            .Where(IsFixAllAction)
            .ToArray();

        Assert.That(fixAllActions, Has.Length.EqualTo(3));
        Assert.That(fixAllActions, Has.All.Matches<CodeAction>(action => action.Kind?.Value == CodeActionKind.QuickFix.Value));

        var documentFixAll = fixAllActions.Single(action => action.Title.Contains("document", StringComparison.OrdinalIgnoreCase));
        var projectFixAll = fixAllActions.Single(action => action.Title.Contains("project", StringComparison.OrdinalIgnoreCase));
        var solutionFixAll = fixAllActions.Single(action => action.Title.Contains("solution", StringComparison.OrdinalIgnoreCase));

        Assert.That(GetChangedFiles(await handler.Resolve(documentFixAll, CancellationToken.None).ConfigureAwait(false)), Is.EquivalentTo(new[] { primaryDocumentPath }));
        Assert.That(GetChangedFiles(await handler.Resolve(projectFixAll, CancellationToken.None).ConfigureAwait(false)), Is.EquivalentTo(new[] { primaryDocumentPath, siblingDocumentPath }));
        Assert.That(GetChangedFiles(await handler.Resolve(solutionFixAll, CancellationToken.None).ConfigureAwait(false)), Is.EquivalentTo(new[] { primaryDocumentPath, siblingDocumentPath, otherProjectDocumentPath }));
    }
    [Test]
    public async Task ResolveFixAllAfterHandleCancellation_ShouldUseResolveToken() {
        var documents = Workspace.Solution!.Projects.SelectMany(project => project.Documents).ToArray();
        await codeAnalysisService.AnalyzeAsync(documents, AnalysisScope.Solution, AnalysisScope.None, CancellationToken.None).ConfigureAwait(false);

        var document = Workspace.Solution.GetDocumentIdsWithFilePath(primaryDocumentPath)
            .Select(id => Workspace.Solution.GetDocument(id))
            .Single()!;

        using var cancellationTokenSource = new CancellationTokenSource();
        var result = await handler.Handle(new CodeActionParams {
            TextDocument = document.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(0, 0, 1, 0)
        }, cancellationTokenSource.Token).ConfigureAwait(false);

        cancellationTokenSource.Cancel();

        var documentFixAll = result.CommandOrCodeActions
            .Select(action => action.CodeAction)
            .OfType<CodeAction>()
            .Single(action => action.Title.Contains("document", StringComparison.OrdinalIgnoreCase));

        var resolvedAction = await handler.Resolve(documentFixAll, CancellationToken.None).ConfigureAwait(false);

        Assert.That(resolvedAction?.Edit, Is.Not.Null);
        Assert.That(GetChangedFiles(resolvedAction), Is.EquivalentTo(new[] { primaryDocumentPath }));
    }

    private static bool IsFixAllAction(CodeAction? action) {
        if (action == null)
            return false;

        return action.Title.Contains("document", StringComparison.OrdinalIgnoreCase)
            || action.Title.Contains("project", StringComparison.OrdinalIgnoreCase)
            || action.Title.Contains("solution", StringComparison.OrdinalIgnoreCase);
    }
    private static string[] GetChangedFiles(CodeAction? action) {
        var changes = action?.Edit?.Changes;
        if (changes == null)
            return Array.Empty<string>();

        return changes.Keys
            .Select(uri => Path.GetFullPath(uri.FileSystemPath))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
    private static string CreateSourceFile(string projectFilePath, string documentName) {
        var projectDirectory = Path.GetDirectoryName(projectFilePath)!;
        var documentPath = Path.Combine(projectDirectory, $"{documentName}.cs");
        File.WriteAllText(documentPath, $@"using System;
using System.Text.Json;

namespace Tests;

internal static class {documentName} {{
    public static string Serialize() {{
        return JsonSerializer.Serialize(1);
    }}
}}");

        return documentPath;
    }
}
