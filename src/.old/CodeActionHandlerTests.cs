using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Tests.Extensions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace DotRush.Roslyn.Tests.HandlersTests.TextDocument;

public class CodeActionHandlerTests : TestFixtureBase, IDisposable {
    private static WorkspaceService WorkspaceService => ServiceProvider.WorkspaceService;
    private static CodeAnalysisService CodeAnalysisService => ServiceProvider.CodeAnalysisService;
    private static ConfigurationService ConfigurationService => ServiceProvider.ConfigurationService;

    private static CodeActionHandler CodeActionHandler = new CodeActionHandler(WorkspaceService, CodeAnalysisService);

    private readonly string documentPath = Path.Combine(ServiceProvider.SharedProjectDirectory, "CodeActionHandlerTest.cs");
    private DocumentUri DocumentUri => DocumentUri.FromFileSystemPath(documentPath);

    [Fact]
    public async Task AutoUsingCodeActionTest() {
        TestProjectExtensions.CreateDocument(documentPath, @"
namespace Tests;
class CodeActionTest {
    private void Method() {
        _ = JsonSerializer.Serialize(1);
    }
}
        ");
        WorkspaceService.CreateDocument(documentPath);
        await CodeAnalysisService.CompilationHost.DiagnoseAsync(WorkspaceService.Solution!.Projects, CancellationToken.None).ConfigureAwait(false);
        var diagnostics = CodeAnalysisService.CompilationHost.GetDiagnostics(documentPath);
        Assert.NotNull(diagnostics);
        Assert.NotEmpty(diagnostics);
        var errorDiagnostic = diagnostics.Single(it => it.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

        var result = await CodeActionHandler.Handle(new CodeActionParams() {
            TextDocument = new TextDocumentIdentifier() { Uri = DocumentUri },
            Context = new CodeActionContext() {
                Diagnostics = new Container<Diagnostic>(errorDiagnostic.ToServerDiagnostic())
            }
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(result);
        Assert.Equal(9, result.Count());
        foreach (var codeAction in result) {
            Assert.NotNull(codeAction.CodeAction);
            Assert.Equal(CodeActionKind.QuickFix, codeAction.CodeAction!.Kind);
        }
        Assert.Contains(result, it => it.CodeAction!.Title == "using System.Text.Json;");
        Assert.Contains(result, it => it.CodeAction!.Title == "Generate property 'JsonSerializer'");
        Assert.Contains(result, it => it.CodeAction!.Title == "Generate field 'JsonSerializer'");
        Assert.Contains(result, it => it.CodeAction!.Title == "Generate read-only field 'JsonSerializer'");
        Assert.Contains(result, it => it.CodeAction!.Title == "Generate local 'JsonSerializer'");
        Assert.Contains(result, it => it.CodeAction!.Title == "Generate parameter 'JsonSerializer'");
        Assert.Contains(result, it => it.CodeAction!.Title == "Generate class 'JsonSerializer'");
        Assert.Contains(result, it => it.CodeAction!.Title == "Generate nested class 'JsonSerializer'");
        Assert.Contains(result, it => it.CodeAction!.Title == "System.Text.Json.JsonSerializer");

        var autoUsingCodeAction = result.Single(it => it.CodeAction!.Title == "using System.Text.Json;");
        var resolvedResult = await CodeActionHandler.Handle(autoUsingCodeAction.CodeAction!, CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(resolvedResult);
        Assert.NotNull(resolvedResult.Edit);
        Assert.NotNull(resolvedResult.Edit!.DocumentChanges);
        Assert.Single(resolvedResult.Edit!.DocumentChanges);
        var textDocumentEdit = resolvedResult.Edit!.DocumentChanges.Single().TextDocumentEdit;
        Assert.NotNull(textDocumentEdit);
        Assert.Equal(DocumentUri, textDocumentEdit.TextDocument.Uri);
        Assert.NotNull(textDocumentEdit.Edits);
        Assert.Single(textDocumentEdit.Edits);
        var textEdit = textDocumentEdit.Edits.Single();
        Assert.StartsWith("using System.Text.Json;", textEdit.NewText);
    }

    [Fact]
    public async Task ApplyCodeActionInConditionsTest() {
        TestProjectExtensions.CreateDocument(documentPath, @"
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
        WorkspaceService.CreateDocument(documentPath);
        await CodeAnalysisService.CompilationHost.DiagnoseAsync(WorkspaceService.Solution!.Projects, CancellationToken.None).ConfigureAwait(false);

        var diagnostics = CodeAnalysisService.CompilationHost.GetDiagnostics(documentPath);
        Assert.NotNull(diagnostics);
        Assert.NotEmpty(diagnostics);
        var warningDiagnostics = diagnostics.Where(it => it.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
        Assert.Equal(3, warningDiagnostics.Count());

        foreach (var warningDiagnostic in warningDiagnostics) {
            var result = await CodeActionHandler.Handle(new CodeActionParams() {
                TextDocument = new TextDocumentIdentifier() { Uri = DocumentUri },
                Context = new CodeActionContext() {
                    Diagnostics = new Container<Diagnostic>(warningDiagnostic.ToServerDiagnostic())
                }
            }, CancellationToken.None).ConfigureAwait(false);
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.NotNull(result.First().CodeAction);
            Assert.Equal(CodeActionKind.QuickFix, result.First().CodeAction!.Kind);
            Assert.Equal("Remove unused variable", result.First().CodeAction!.Title);

            var resolvedResult = await CodeActionHandler.Handle(result.First().CodeAction!, CancellationToken.None).ConfigureAwait(false);
            Assert.NotNull(resolvedResult);
            Assert.NotNull(resolvedResult.Edit);
            Assert.NotNull(resolvedResult.Edit!.DocumentChanges);
            Assert.Single(resolvedResult.Edit!.DocumentChanges);
            var textDocumentEdit = resolvedResult.Edit!.DocumentChanges.Single().TextDocumentEdit;
            Assert.NotNull(textDocumentEdit);
            Assert.Equal(DocumentUri, textDocumentEdit.TextDocument.Uri);
            Assert.NotNull(textDocumentEdit.Edits);
            Assert.Single(textDocumentEdit.Edits);
            var textEdit = textDocumentEdit.Edits.Single();
            Assert.Equal(string.Empty, textEdit.NewText);
        }
    }

    public void Dispose() {
        WorkspaceService.DeleteDocument(documentPath);
    }
}