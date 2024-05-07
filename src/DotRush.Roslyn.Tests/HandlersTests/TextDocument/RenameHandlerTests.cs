using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Tests.Extensions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace DotRush.Roslyn.Tests.HandlersTests.TextDocument;

public class RenameHandlerTests : TestFixtureBase, IDisposable {
    private static WorkspaceService WorkspaceService => ServiceProvider.WorkspaceService;
    private static RenameHandler RenameHandler = new RenameHandler(WorkspaceService);

    private readonly string documentPath = Path.Combine(ServiceProvider.SharedProjectDirectory, "RenameHandlerTest.cs");
    private readonly string documentPath2 = Path.Combine(ServiceProvider.SharedProjectDirectory, "RenameHandlerTest2.cs");
    private DocumentUri DocumentUri => DocumentUri.FromFileSystemPath(documentPath);
    private DocumentUri DocumentUri2 => DocumentUri.FromFileSystemPath(documentPath2);

    [Fact]
    public async Task RenameSymbolTest() {
        TestProjectExtensions.CreateDocument(documentPath, @"
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
        TestProjectExtensions.CreateDocument(documentPath, @"
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
    [Fact]
    public async Task RenameSymbolInDifferentFilesTest() {
        TestProjectExtensions.CreateDocument(documentPath, @"
namespace Tests;
class RenameHandlerTest {
    private void MainMethod() {
        SomeClass.TestMethod();
    }
}
        ");
        TestProjectExtensions.CreateDocument(documentPath2, @"
namespace Tests;
class SomeClass {
    private static void TestMethod() {
    }
}
        ");
        WorkspaceService.CreateDocument(documentPath);
        WorkspaceService.CreateDocument(documentPath2);
        var result = await RenameHandler.Handle(new RenameParams() {
            Position = new Position() {
                Line = 4,
                Character = 20,
            },
            NewName = "TestMethodNew",
            TextDocument = new TextDocumentIdentifier() { Uri = DocumentUri },
        }, CancellationToken.None).ConfigureAwait(false);
        Assert.NotNull(result);
        Assert.Equal(2, result.Changes!.Count);

        Assert.Single(result.Changes[DocumentUri]);
        Assert.Equal("New", result.Changes[DocumentUri].Single().NewText);

        Assert.Single(result.Changes[DocumentUri2]);
        Assert.Equal("New", result.Changes[DocumentUri2].Single().NewText);
    }

    public void Dispose() {
        WorkspaceService.DeleteDocument(documentPath);
        WorkspaceService.DeleteDocument(documentPath2);
    }
}