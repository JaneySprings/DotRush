using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.CodeAction;
using EmmyLua.LanguageServer.Framework.Protocol.Model.File;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class CodeActionV2HandlerMock : CodeActionV2Handler {
    public CodeActionV2HandlerMock(WorkspaceService workspaceService, CodeAnalysisService codeAnalysisService) : base(workspaceService, codeAnalysisService) { }

    public new Task<CodeActionResponse> Handle(CodeActionParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
    public new Task<CodeAction?> Resolve(CodeAction request, CancellationToken token) {
        return base.Resolve(request, token);
    }
}

public class CodeActionV2HandlerTests : MultitargetProjectFixture {
    private CodeAnalysisService codeAnalysisService;
    private CodeActionV2HandlerMock handler;

    [SetUp]
    public void SetUp() {
        codeAnalysisService = new CodeAnalysisService(new ConfigurationService(null), null);
        handler = new CodeActionV2HandlerMock(Workspace, codeAnalysisService);
    }

    [Test]
    public async Task GeneralHandlerTest() {
        var documents = CreateAndGetDocuments(nameof(CodeActionV2HandlerTests), @"
namespace Tests;
class CodeActionTest {
    private void Method() {
        JsonSerializer.Serialize(1);
    }
}
");
        await codeAnalysisService.AnalyzeAsync(documents, AnalysisScope.Document, AnalysisScope.None, CancellationToken.None);
        var result = await handler.Handle(new CodeActionParams() {
            TextDocument = documents.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(4, 5)
        }, CancellationToken.None);

        Assert.That(result.CommandOrCodeActions, Is.Not.Null.Or.Empty);
        Assert.That(result.CommandOrCodeActions, Has.Count.EqualTo(14));
        // QuickFix
        Assert.That(GetCodeAction(result, "using System.Text.Json;"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Generate property 'JsonSerializer'"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Generate field 'JsonSerializer'"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Generate read-only field 'JsonSerializer'"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Generate local 'JsonSerializer'"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Generate parameter 'JsonSerializer'"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Generate class 'JsonSerializer'"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Generate class 'JsonSerializer' in new file"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Generate nested class 'JsonSerializer'"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "System.Text.Json.JsonSerializer"), Is.Not.Null);
        // Refactor
        Assert.That(GetCodeAction(result, "Extract method"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Extract local function"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Use expression body for method"), Is.Not.Null);

        var usingAction = GetCodeAction(result, "using System.Text.Json;");
        Assert.That(usingAction.Kind, Is.EqualTo(CodeActionKind.QuickFix));
        Assert.That(usingAction.IsPreferred, Is.True);
        Assert.That(usingAction.Data, Is.Not.Null);

        var resolvedResult = await handler.Resolve(usingAction, CancellationToken.None);
        Assert.That(resolvedResult!.Edit, Is.Not.Null);
        Assert.That(resolvedResult.Edit.Changes, Is.Null);
        Assert.That(resolvedResult.Edit.DocumentChanges?.EditFileList, Has.Count.EqualTo(1));
        var documentEdit = resolvedResult.Edit.DocumentChanges.EditFileList[0] as TextDocumentEdit;
        Assert.That(documentEdit, Is.Not.Null);
        Assert.That(documentEdit!.TextDocument.Uri.FileSystemPath, Is.EqualTo(documents.First().FilePath));
        Assert.That(documentEdit.Edits.TextEditList, Has.Count.EqualTo(1));
        Assert.That(documentEdit.Edits.TextEditList![0].NewText.ToLF(), Is.EqualTo("using System.Text.Json;\n\n"));
        Assert.That(documentEdit.Edits.TextEditList[0].Range, Is.EqualTo(PositionExtensions.CreateRange(1, 0, 1, 0)));
    }
    [TestCase("quickfix", 10)]
    [TestCase("refactor", 4)]
    [TestCase("source", 0)]
    public async Task GeneralHandlerWithFilterTest(string kind, int expectedCount) {
        var documents = CreateAndGetDocuments(nameof(CodeActionV2HandlerTests), @"
namespace Tests;
class CodeActionTest {
    private void Method() {
        JsonSerializer.Serialize(1);
    }
}
");
        await codeAnalysisService.AnalyzeAsync(documents, AnalysisScope.Document, AnalysisScope.None, CancellationToken.None);
        var result = await handler.Handle(new CodeActionParams() {
            TextDocument = documents.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(4, 5),
            Context = new CodeActionContext { Only = new List<CodeActionKind> { new CodeActionKind(kind) } }
        }, CancellationToken.None);

        Assert.That(result.CommandOrCodeActions, Is.Not.Null.Or.Empty);
        Assert.That(result.CommandOrCodeActions, Has.Count.EqualTo(expectedCount));
        result.CommandOrCodeActions.ForEach(ca => Assert.That(ca.CodeAction, Is.Not.Null));
    }

    [Test]
    public async Task HandleDocumentRenameTest() {
        var document = CreateDocument(nameof(CodeActionV2HandlerTests), @"
namespace Tests;
class CodeActionTest {
}
");
        var result = await handler.Handle(new CodeActionParams() {
            TextDocument = document.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(2, 7, 2, 18),
            Context = new CodeActionContext { Only = new List<CodeActionKind> { CodeActionKind.Refactor } }
        }, CancellationToken.None);

        Assert.That(result.CommandOrCodeActions, Is.Not.Null.Or.Empty);
        Assert.That(result.CommandOrCodeActions, Has.Count.EqualTo(4));

        Assert.That(GetCodeAction(result, "Generate description in XML"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Rename file to CodeActionTest.cs"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Rename type to CodeActionV2HandlerTests"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Add 'DebuggerDisplay' attribute"), Is.Not.Null);

        var renameAction = await handler.Resolve(GetCodeAction(result, "Rename file to CodeActionTest.cs"), CancellationToken.None);
        Assert.That(renameAction!.Edit, Is.Not.Null);
        Assert.That(renameAction.Edit.Changes, Is.Null);
        Assert.That(renameAction.Edit.DocumentChanges?.EditFileList, Has.Count.EqualTo(1));
        var renameEdit = renameAction.Edit.DocumentChanges.EditFileList[0] as RenameFile;
        Assert.That(renameEdit, Is.Not.Null);
        Assert.That(renameEdit!.Options, Is.Null);
        Assert.That(renameEdit.AnnotationId, Is.Null);
        Assert.That(renameEdit.OldUri.FileSystemPath, Is.EqualTo(document));
        Assert.That(renameEdit.NewUri.FileSystemPath, Is.EqualTo(Path.Combine(Path.GetDirectoryName(document)!, "CodeActionTest.cs")));
    }
    [Test]
    public async Task HandleDocumentCreateTest() {
        var document = CreateDocument(nameof(CodeActionV2HandlerTests), @"
namespace Tests;
class CodeActionTest {
}
class MyClass {
}
");
        var result = await handler.Handle(new CodeActionParams() {
            TextDocument = document.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(4, 7, 4, 12),
            Context = new CodeActionContext { Only = new List<CodeActionKind> { CodeActionKind.Refactor } }
        }, CancellationToken.None);

        Assert.That(result.CommandOrCodeActions, Is.Not.Null.Or.Empty);
        Assert.That(result.CommandOrCodeActions, Has.Count.EqualTo(5));

        Assert.That(GetCodeAction(result, "Generate description in XML"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Move type to MyClass.cs"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Rename file to MyClass.cs"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Rename type to CodeActionV2HandlerTests"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Add 'DebuggerDisplay' attribute"), Is.Not.Null);

        var moveAction = await handler.Resolve(GetCodeAction(result, "Move type to MyClass.cs"), CancellationToken.None);
        Assert.That(moveAction!.Edit, Is.Not.Null);
        Assert.That(moveAction.Edit.Changes, Is.Null);
        Assert.That(moveAction.Edit.DocumentChanges?.EditFileList, Has.Count.EqualTo(3));

        var removeOldEdit = moveAction.Edit.DocumentChanges.EditFileList[0] as TextDocumentEdit;
        Assert.That(removeOldEdit, Is.Not.Null);
        Assert.That(removeOldEdit!.TextDocument.Uri.FileSystemPath, Is.EqualTo(document));
        Assert.That(removeOldEdit.Edits.TextEditList, Has.Count.EqualTo(1));
        Assert.That(removeOldEdit.Edits.TextEditList![0].NewText, Is.Empty);
        Assert.That(removeOldEdit.Edits.TextEditList[0].Range, Is.EqualTo(PositionExtensions.CreateRange(4, 0, 6, 0)));

        var createEdit = moveAction.Edit.DocumentChanges.EditFileList[1] as CreateFile;
        Assert.That(createEdit, Is.Not.Null);
        Assert.That(createEdit!.Options, Is.Not.Null);
        Assert.That(createEdit.Options.Value.Overwrite, Is.True);
        Assert.That(createEdit.Options.Value.IgnoreIfExists, Is.False);
        Assert.That(createEdit.AnnotationId, Is.Null);
        Assert.That(createEdit.Uri.FileSystemPath, Is.EqualTo(Path.Combine(Path.GetDirectoryName(document)!, "MyClass.cs")));

        var addNewEdit = moveAction.Edit.DocumentChanges.EditFileList[2] as TextDocumentEdit;
        Assert.That(addNewEdit, Is.Not.Null);
        Assert.That(addNewEdit!.TextDocument.Uri.FileSystemPath, Is.EqualTo(Path.Combine(Path.GetDirectoryName(document)!, "MyClass.cs")));
        Assert.That(addNewEdit.Edits.TextEditList, Has.Count.EqualTo(1));
        Assert.That(addNewEdit.Edits.TextEditList![0].NewText.ToLF(), Is.EqualTo("\nnamespace Tests;\n\nclass MyClass {\n}\n"));
        Assert.That(addNewEdit.Edits.TextEditList[0].Range, Is.EqualTo(PositionExtensions.CreateRange(0, 0, 0, 0)));
    }

    [TestCase(5, 6)]
    [TestCase(7, 8)]
    [TestCase(9, 10)]
    public async Task HandleConditionalDirectivesTest(int startLine, int endLine) {
        var documents = CreateAndGetDocuments(nameof(CodeActionV2HandlerTests), @"
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
        await codeAnalysisService.AnalyzeAsync(documents, AnalysisScope.Document, AnalysisScope.None, CancellationToken.None);
        var result = await handler.Handle(new CodeActionParams() {
            TextDocument = documents.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(startLine, endLine),
            Context = new CodeActionContext { Only = new List<CodeActionKind> { CodeActionKind.QuickFix } }
        }, CancellationToken.None);

        Assert.That(result.CommandOrCodeActions, Has.Count.EqualTo(1));

        var resolvedResult = await handler.Resolve(GetCodeAction(result, "Remove unused variable")!, CancellationToken.None);
        Assert.That(resolvedResult!.Edit, Is.Not.Null);
        Assert.That(resolvedResult.Edit.Changes, Is.Null);
        Assert.That(resolvedResult.Edit.DocumentChanges?.EditFileList, Has.Count.EqualTo(1));
        var documentEdit = resolvedResult.Edit.DocumentChanges.EditFileList[0] as TextDocumentEdit;
        Assert.That(documentEdit, Is.Not.Null);
        Assert.That(documentEdit!.TextDocument.Uri.FileSystemPath, Is.EqualTo(documents[0].FilePath));
        Assert.That(documentEdit.Edits.TextEditList, Has.Count.EqualTo(1));
        Assert.That(documentEdit.Edits.TextEditList![0].NewText, Is.Empty);
    }

    [Test]
    public async Task ApplyFixAllInNoneScopeTest() {
        var documents = CreateAndGetDocuments(nameof(CodeActionV2HandlerTests), @"
namespace Tests;
sealed class CodeActionTest {
    private static void Method() {
        var test = 1;
    }
}
");
        await codeAnalysisService.AnalyzeAsync(documents, AnalysisScope.None, AnalysisScope.None, CancellationToken.None);
        var result = await handler.Handle(new CodeActionParams() {
            TextDocument = documents.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(4, 5),
            Context = new CodeActionContext { Only = new List<CodeActionKind> { CodeActionKind.QuickFix } }
        }, CancellationToken.None);

        Assert.That(result.CommandOrCodeActions, Is.Null.Or.Empty);
    }
    [Test]
    public async Task ApplyFixAllInDocumentScopeTest() {
        const string caTitle = "Fix all 'CS0219' in 'CodeActionV2HandlerTests.cs'";
        var documents = CreateAndGetDocuments(nameof(CodeActionV2HandlerTests), @"
namespace Tests;
sealed class CodeActionTest {
    private static void Method() {
        var test = 1;
    }
    private static void Method2() {
        var test2 = 2;
    }
}
");
        await codeAnalysisService.AnalyzeAsync(documents, AnalysisScope.Document, AnalysisScope.None, CancellationToken.None);
        var result = await handler.Handle(new CodeActionParams() {
            TextDocument = documents.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(4, 5),
            Context = new CodeActionContext { Only = new List<CodeActionKind> { CodeActionKind.QuickFix } }
        }, CancellationToken.None);

        Assert.That(result.CommandOrCodeActions, Has.Count.EqualTo(2));
        Assert.That(GetCodeAction(result, "Remove unused variable"), Is.Not.Null);
        Assert.That(GetCodeAction(result, caTitle), Is.Not.Null);

        var resolvedResult = await handler.Resolve(GetCodeAction(result, caTitle), CancellationToken.None);
        Assert.That(resolvedResult!.Edit, Is.Not.Null);
        Assert.That(resolvedResult.Edit.Changes, Is.Null);
        Assert.That(resolvedResult.Edit.DocumentChanges?.EditFileList, Has.Count.EqualTo(1));
        var documentEdit = resolvedResult.Edit.DocumentChanges.EditFileList[0] as TextDocumentEdit;
        Assert.That(documentEdit, Is.Not.Null);
        Assert.That(documentEdit!.TextDocument.Uri.FileSystemPath, Is.EqualTo(documents[0].FilePath));
        Assert.That(documentEdit.Edits.TextEditList, Has.Count.EqualTo(2));
        Assert.That(documentEdit.Edits.TextEditList[0].NewText, Is.Empty);
        Assert.That(documentEdit.Edits.TextEditList[0].Range, Is.EqualTo(PositionExtensions.CreateRange(4, 4, 5, 4)));
        Assert.That(documentEdit.Edits.TextEditList[1].NewText, Is.Empty);
        Assert.That(documentEdit.Edits.TextEditList[1].Range, Is.EqualTo(PositionExtensions.CreateRange(7, 4, 8, 4)));
    }
    [Test]
    public async Task ApplyFixAllInProjectScopeTest() {
        var caProjectTitle = $"Fix all 'CS0219' in '{ProjectName}(net8.0)'";
        var caProjectTitle2 = $"Fix all 'CS0219' in '{ProjectName}(net10.0)'";
        var documents = CreateAndGetDocuments(nameof(CodeActionV2HandlerTests), @"
namespace Tests;
sealed class CodeActionTest {
    private static void Method() {
        var test = 1;
    }
}
");
        var documents2 = CreateAndGetDocuments(nameof(CodeActionV2HandlerTests) + "2", @"
namespace Tests;
sealed class CodeActionSecondTest {
    private static void DoSome() {
        var test2 = 1;
    }
}
");
        await codeAnalysisService.AnalyzeAsync(documents2, AnalysisScope.Project, AnalysisScope.None, CancellationToken.None);
        var result = await handler.Handle(new CodeActionParams() {
            TextDocument = documents.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(4, 5),
            Context = new CodeActionContext { Only = new List<CodeActionKind> { CodeActionKind.QuickFix } }
        }, CancellationToken.None);

        Assert.That(result.CommandOrCodeActions, Has.Count.EqualTo(4));
        Assert.That(GetCodeAction(result, "Remove unused variable"), Is.Not.Null);
        Assert.That(GetCodeAction(result, "Fix all 'CS0219' in 'CodeActionV2HandlerTests.cs'"), Is.Not.Null);
        Assert.That(GetCodeAction(result, caProjectTitle), Is.Not.Null);
        Assert.That(GetCodeAction(result, caProjectTitle2), Is.Not.Null);

        var resolvedResult = await handler.Resolve(GetCodeAction(result, caProjectTitle), CancellationToken.None);
        Assert.That(resolvedResult!.Edit, Is.Not.Null);
        Assert.That(resolvedResult.Edit.Changes, Is.Null);
        Assert.That(resolvedResult.Edit.DocumentChanges?.EditFileList, Has.Count.EqualTo(2));

        var documentEdit = resolvedResult.Edit.DocumentChanges.EditFileList[0] as TextDocumentEdit;
        Assert.That(documentEdit, Is.Not.Null);
        Assert.That(documentEdit!.TextDocument.Uri.FileSystemPath, Is.EqualTo(documents[0].FilePath));
        Assert.That(documentEdit.Edits.TextEditList, Has.Count.EqualTo(1));
        Assert.That(documentEdit.Edits.TextEditList![0].NewText, Is.Empty);
        Assert.That(documentEdit.Edits.TextEditList[0].Range, Is.EqualTo(PositionExtensions.CreateRange(4, 4, 5, 4)));

        var documentEdit2 = resolvedResult.Edit.DocumentChanges.EditFileList[1] as TextDocumentEdit;
        Assert.That(documentEdit2, Is.Not.Null);
        Assert.That(documentEdit2!.TextDocument.Uri.FileSystemPath, Is.EqualTo(documents2[0].FilePath));
        Assert.That(documentEdit2.Edits.TextEditList, Has.Count.EqualTo(1));
        Assert.That(documentEdit2.Edits.TextEditList![0].NewText, Is.Empty);
        Assert.That(documentEdit2.Edits.TextEditList[0].Range, Is.EqualTo(PositionExtensions.CreateRange(4, 4, 5, 4)));
    }

    private static CodeAction? GetCodeAction(CodeActionResponse response, string title) {
        return response.CommandOrCodeActions.SingleOrDefault(ca => ca.CodeAction?.Title == title)?.CodeAction;
    }
}
