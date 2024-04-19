using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Tests.Extensions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace DotRush.Roslyn.Tests.HandlersTests.TextDocument;

public class RenameHandlerTests : TestFixtureBase, IDisposable {
    private static WorkspaceService WorkspaceService => ServiceProvider.WorkspaceService;
    private static RenameHandler RenameHandler => new RenameHandler(WorkspaceService);

    private string documentPath = string.Empty;
    private DocumentUri DocumentUri => DocumentUri.FromFileSystemPath(documentPath);

    [Fact]
    public async Task RenameSymbolTest() {
        documentPath = TestProjectExtensions.CreateDocument(ServiceProvider.SharedProjectPath, "RenameHandlerTest.cs", @"
namespace Tests;
class RenameHandlerTest {
    private void MainMethod() {
        TestMethod();
    }
    private void TestMethod() {
    }
}
        ");
        WorkspaceService.CreateDocument(documentPath);
        var result = await RenameHandler.Handle(new RenameParams() {
            Position = new Position() {
                Line = 4,
                Character = 10,
            },
            NewName = "TestMethodNew",
            TextDocument = new TextDocumentIdentifier() { Uri = DocumentUri },
        }, CancellationToken.None).ConfigureAwait(false);
        Assert.NotNull(result);
        Assert.Equal(1, result.Changes!.Count);
        Assert.Equal(2, result.Changes[DocumentUri].Count());
        foreach (var change in result.Changes[DocumentUri])
            Assert.Equal("New", change.NewText);
    }
    [Fact]
    public async Task RenameSymbolInsideDirectivesTest() {
        documentPath = TestProjectExtensions.CreateDocument(ServiceProvider.SharedProjectPath, "RenameHandlerTest.cs", @"
namespace Tests;
class RenameHandlerTest {
    private void MainMethod() {
        TestMethod();
    }
#if NET8_0
    private void TestMethod() {
    }
#else
    private void TestMethod() {
    }
#endif
}
        ");
        WorkspaceService.CreateDocument(documentPath);
        var result = await RenameHandler.Handle(new RenameParams() {
            Position = new Position() {
                Line = 4,
                Character = 10,
            },
            NewName = "TestMethodNew",
            TextDocument = new TextDocumentIdentifier() { Uri = DocumentUri },
        }, CancellationToken.None).ConfigureAwait(false);
        Assert.NotNull(result);
        Assert.Equal(1, result.Changes!.Count);
        Assert.Equal(3, result.Changes[DocumentUri].Count());
        foreach (var change in result.Changes[DocumentUri])
            Assert.Equal("New", change.NewText);
    }

    public void Dispose() {
        WorkspaceService.DeleteDocument(documentPath);
    }
}