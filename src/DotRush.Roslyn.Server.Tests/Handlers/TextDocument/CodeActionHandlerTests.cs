using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.CodeAction;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class CodeActionHandlerMock : CodeActionHandler {
    public CodeActionHandlerMock(WorkspaceService workspaceService, CodeAnalysisService codeAnalysisService) : base(workspaceService, codeAnalysisService) { }

    public new Task<CodeActionResponse> Handle(CodeActionParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
    public new Task<CodeAction> Resolve(CodeAction request, CancellationToken token) {
        return base.Resolve(request, token);
    }
}

public class CodeActionHandlerTests : MultitargetProjectFixture {
    private CodeAnalysisService codeAnalysisService;
    private CodeActionHandlerMock handler;

    [SetUp]
    public void SetUp() {
        codeAnalysisService = new CodeAnalysisService(new ConfigurationService(null), null);
        handler = new CodeActionHandlerMock(Workspace, codeAnalysisService);
    }

    [Test]
    public async Task GeneralHandlerTest() {
        var documents = CreateAndGetDocuments(nameof(CodeActionHandlerTests), @"
namespace Tests;
class CodeActionTest {
    private void Method() {
        _ = JsonSerializer.Serialize(1);
    }
}
");
        await codeAnalysisService.AnalyzeAsync(documents, AnalysisScope.Document, AnalysisScope.None, CancellationToken.None).ConfigureAwait(false);
        var result = await handler.Handle(new CodeActionParams() {
            TextDocument = documents.First().CreateDocumentId(),
            Range = PositionExtensions.CreateRange(4, 5)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result.CommandOrCodeActions, Is.Not.Null.Or.Empty);
        Assert.That(result.CommandOrCodeActions, Has.Count.EqualTo(13));
        result.CommandOrCodeActions.ForEach(ca => Assert.That(ca.CodeAction, Is.Not.Null));
        // QuickFix
        Assert.That(result.CommandOrCodeActions, Has.One.Matches<CommandOrCodeAction>(ca => ca.CodeAction!.Title == "using System.Text.Json;"));
        Assert.That(result.CommandOrCodeActions, Has.One.Matches<CommandOrCodeAction>(ca => ca.CodeAction!.Title == "Generate property 'JsonSerializer'"));
        Assert.That(result.CommandOrCodeActions, Has.One.Matches<CommandOrCodeAction>(ca => ca.CodeAction!.Title == "Generate field 'JsonSerializer'"));
        Assert.That(result.CommandOrCodeActions, Has.One.Matches<CommandOrCodeAction>(ca => ca.CodeAction!.Title == "Generate read-only field 'JsonSerializer'"));
        Assert.That(result.CommandOrCodeActions, Has.One.Matches<CommandOrCodeAction>(ca => ca.CodeAction!.Title == "Generate local 'JsonSerializer'"));
        Assert.That(result.CommandOrCodeActions, Has.One.Matches<CommandOrCodeAction>(ca => ca.CodeAction!.Title == "Generate parameter 'JsonSerializer'"));
        Assert.That(result.CommandOrCodeActions, Has.One.Matches<CommandOrCodeAction>(ca => ca.CodeAction!.Title == "Generate class 'JsonSerializer'"));
        Assert.That(result.CommandOrCodeActions, Has.One.Matches<CommandOrCodeAction>(ca => ca.CodeAction!.Title == "Generate class 'JsonSerializer' in new file"));
        Assert.That(result.CommandOrCodeActions, Has.One.Matches<CommandOrCodeAction>(ca => ca.CodeAction!.Title == "Generate nested class 'JsonSerializer'"));
        Assert.That(result.CommandOrCodeActions, Has.One.Matches<CommandOrCodeAction>(ca => ca.CodeAction!.Title == "System.Text.Json.JsonSerializer"));
        // Refactor
        Assert.That(result.CommandOrCodeActions, Has.One.Matches<CommandOrCodeAction>(ca => ca.CodeAction!.Title == "Extract method"));
        Assert.That(result.CommandOrCodeActions, Has.One.Matches<CommandOrCodeAction>(ca => ca.CodeAction!.Title == "Extract local function"));
        Assert.That(result.CommandOrCodeActions, Has.One.Matches<CommandOrCodeAction>(ca => ca.CodeAction!.Title == "Use expression body for method"));

        var usingCodeAction = result.CommandOrCodeActions.Single(it => it.CodeAction!.Title == "using System.Text.Json;");
        var resolvedResult = await handler.Resolve(usingCodeAction.CodeAction!, CancellationToken.None).ConfigureAwait(false);

        Assert.That(resolvedResult?.Edit, Is.Not.Null);
        Assert.That(resolvedResult.Edit.Changes, Has.Count.EqualTo(1));
        var textDocumentEdit = resolvedResult.Edit.Changes.First();
        Assert.That(PathExtensions.Equals(textDocumentEdit.Key.FileSystemPath, documents.First().FilePath), Is.True);
        Assert.That(textDocumentEdit.Value, Has.Count.EqualTo(1));
        Assert.That(textDocumentEdit.Value[0].NewText, Does.StartWith("using System.Text.Json;"));
    }

    [TestCase(5, 6)]
    [TestCase(7, 8)]
    [TestCase(9, 10)]
    public async Task ApplyCodeActionInConditionsTest(int startLine, int endLine) {
        var documents = CreateAndGetDocuments(nameof(CodeActionHandlerTests), @"
namespace Tests;
sealed class CodeActionTest {
    private static void Method() {
#if NET8_0
        var test = 1;
#else
        var test = 2;
#endif
        var test2 = 3;
    }
}
");
        await codeAnalysisService.AnalyzeAsync(documents, AnalysisScope.Document, AnalysisScope.None, CancellationToken.None).ConfigureAwait(false);
        var result = await handler.Handle(new CodeActionParams() {
            TextDocument = documents.First().CreateDocumentId(),
            Range = PositionExtensions.CreateRange(startLine, endLine)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result.CommandOrCodeActions, Is.Not.Null.Or.Empty);
        Assert.That(result.CommandOrCodeActions, Has.One.Matches<CommandOrCodeAction>(ca => ca.CodeAction!.Title == "Remove unused variable"));

        var removeCodeAction = result.CommandOrCodeActions.Single(it => it.CodeAction!.Title == "Remove unused variable");
        var resolvedResult = await handler.Resolve(removeCodeAction.CodeAction!, CancellationToken.None).ConfigureAwait(false);

        Assert.That(resolvedResult?.Edit, Is.Not.Null);
        Assert.That(resolvedResult.Edit.Changes, Has.Count.EqualTo(1));
        var textDocumentEdit = resolvedResult.Edit.Changes.First();
        Assert.That(textDocumentEdit.Value, Has.Count.EqualTo(1));
        Assert.That(textDocumentEdit.Value[0].NewText, Is.Empty);
    }
}
