using DotRush.Protocol;
using DotRush.Protocol.Models;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class RenameHandlerMock : RenameHandler {
    public RenameHandlerMock(WorkspaceService workspaceService) : base(workspaceService) { }

    public new Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
    public new Task<DocumentRange?> Handle(PrepareRenameParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
}

public class RenameHandlerTests : MultitargetProjectFixture {
    private RenameHandlerMock handler;

    [SetUp]
    public void SetUp() {
        handler = new RenameHandlerMock(Workspace);
    }

    [Test]
    public async Task GeneralHandlerTest() {
        var documentPath = CreateDocument(nameof(RenameHandlerTests), @"
namespace Tests;
class RenameHandlerTest {
    private void MainMethod() {
        TestMethod();
    }
    private void TestMethod() {
    }
}");
        var result = await handler.Handle(new RenameParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 10),
            NewName = "TestMethodNew"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result?.Changes, Is.Not.Null);
        Assert.That(result.Changes, Has.Count.EqualTo(1));
        var changes = result.Changes[documentPath];
        Assert.That(changes, Has.Count.EqualTo(2));
        Assert.That(changes, Has.One.Matches<TextEdit>(x => x.NewText == "New" && x.Range == PositionExtensions.CreateRange(4, 18, 4, 18)));
        Assert.That(changes, Has.One.Matches<TextEdit>(x => x.NewText == "New" && x.Range == PositionExtensions.CreateRange(6, 27, 6, 27)));
    }
    [Test]
    public async Task PrepareRenameTest() {
        var documentPath = CreateDocument(nameof(RenameHandlerTests), @"
namespace Tests;
class RenameHandlerTest {
    private void MainMethod() {
        TestMethod();
        undeclared = 10;
        var text = string.Empty;
    }
    private void TestMethod() {
    }
}");
        var result = await handler.Handle(new PrepareRenameParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 10)
        }, CancellationToken.None).ConfigureAwait(false);
        Assert.That(result, Is.EqualTo(PositionExtensions.CreateRange(4, 8, 4, 18)));

        // Undeclared and metadata symbols can't be renamed, the client shows its own message for a null result
        result = await handler.Handle(new PrepareRenameParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 10)
        }, CancellationToken.None).ConfigureAwait(false);
        Assert.That(result, Is.Null);

        result = await handler.Handle(new PrepareRenameParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(6, 27)
        }, CancellationToken.None).ConfigureAwait(false);
        Assert.That(result, Is.Null);

        var edit = await handler.Handle(new RenameParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 10),
            NewName = "declared"
        }, CancellationToken.None).ConfigureAwait(false);
        Assert.That(edit, Is.Null);

        var exception = Assert.ThrowsAsync<ProtocolException>(() => handler.Handle(new RenameParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 10),
            NewName = "1 bad name"
        }, CancellationToken.None));
        Assert.That(exception.Message, Is.EqualTo("'1 bad name' is not a valid identifier."));
    }
    [Test]
    public async Task RenameSymbolInsideDirectivesTest() {
        var documentPath = CreateDocument(nameof(RenameHandlerTests), @"
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
}");
        var result = await handler.Handle(new RenameParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 10),
            NewName = "TestMethodNew"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result?.Changes, Is.Not.Null);
        Assert.That(result.Changes, Has.Count.EqualTo(1));
        var changes = result.Changes[documentPath];
        Assert.That(changes, Has.Count.EqualTo(3));
        changes.ForEach(x => Assert.That(x.NewText, Is.EqualTo("New")));
    }
    [Test]
    public async Task RenameSymbolInDifferentFilesTest() {
        var documentPath = CreateDocument(nameof(RenameHandlerTests), @"
namespace Tests2;
class RenameHandlerTest {
    private void MainMethod() {
        SomeClass.TestMethod();
    }
}");
        var documentPath2 = CreateDocument("RenameHandlerTests2", @"
namespace Tests2;
class SomeClass {
    private static void TestMethod() {
    }
}");

        var result = await handler.Handle(new RenameParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 20),
            NewName = "TestMethodNew"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result?.Changes, Is.Not.Null);
        Assert.That(result.Changes, Has.Count.EqualTo(2));
        var changes1 = result.Changes[documentPath];
        Assert.That(changes1, Has.Count.EqualTo(1));
        Assert.That(changes1[0].NewText, Is.EqualTo("New"));

        var changes2 = result.Changes[documentPath2];
        Assert.That(changes2, Has.Count.EqualTo(1));
        Assert.That(changes2[0].NewText, Is.EqualTo("New"));
    }
}