using DotRush.Protocol.Models;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class DocumentFormattingHandlerMock : DocumentFormattingHandler {
    public DocumentFormattingHandlerMock(WorkspaceService workspaceService) : base(workspaceService) { }

    public new Task<List<TextEdit>?> Handle(DocumentFormattingParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
    public new Task<List<TextEdit>?> Handle(DocumentRangeFormattingParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
}

public class DocumentFormattingHandlerTests : MultitargetProjectFixture {
    private DocumentFormattingHandlerMock handler;

    [SetUp]
    public void SetUp() {
        handler = new DocumentFormattingHandlerMock(Workspace);
    }

    [Test]
    public async Task GeneralHandlerTest() {
        var documentPath = CreateDocument(nameof(DocumentFormattingHandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Method1() {
        var a = new 
        object();
    }
}
");
        var result = await handler.Handle(new DocumentFormattingParams() {
            TextDocument = documentPath.CreateDocumentId(),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(3));

        Assert.That(result[0].Range, Is.EqualTo(PositionExtensions.CreateRange(3, 14, 3, 15)));
        Assert.That(result[0].NewText.ToLF(), Is.EqualTo("\n"));
        Assert.That(result[1].Range, Is.EqualTo(PositionExtensions.CreateRange(4, 26, 4, 26)));
        Assert.That(result[1].NewText.ToLF(), Is.EqualTo("\n   "));
        Assert.That(result[2].Range, Is.EqualTo(PositionExtensions.CreateRange(5, 19, 5, 20)));
        Assert.That(result[2].NewText.ToLF(), Is.EqualTo(string.Empty));
    }
    [Test]
    public async Task GeneralHandlerWithRangeTest() {
        var documentPath = CreateDocument(nameof(DocumentFormattingHandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Method1() {
        var a = new object();
    }
}
");
        var result = await handler.Handle(new DocumentRangeFormattingParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(4, 0, 7, 0)
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Range, Is.EqualTo(PositionExtensions.CreateRange(4, 26, 4, 26)));
        Assert.That(result[0].NewText.ToLF(), Is.EqualTo("\n   "));
    }

    [Test]
    public async Task FormatDocumentInsideDirectivesTest() {
        var documentPath = CreateDocument(nameof(DocumentFormattingHandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Method1()
    {
#if NET8_0
var a = new object();
#endif
    }
    private void Method2()
    {
#if !NET8_0
var b = new object();
#endif
    }
}
");
        var result = await handler.Handle(new DocumentFormattingParams() {
            TextDocument = documentPath.CreateDocumentId(),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(3));

        Assert.That(result[0].Range, Is.EqualTo(PositionExtensions.CreateRange(3, 14, 3, 15)));
        Assert.That(result[0].NewText.ToLF(), Is.EqualTo("\n"));
        Assert.That(result[1].Range, Is.EqualTo(PositionExtensions.CreateRange(7, 0, 7, 0)));
        Assert.That(result[1].NewText.ToLF(), Is.EqualTo("        "));
        Assert.That(result[2].Range, Is.EqualTo(PositionExtensions.CreateRange(13, 0, 13, 0)));
        Assert.That(result[2].NewText.ToLF(), Is.EqualTo("        "));
    }
    [Test]
    public async Task FormatDocumentInsideDirectivesWithCollisionTest() {
        var documentPath = CreateDocument(nameof(DocumentFormattingHandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Method1() 
    {
#if NET8_0
var a = new object();
#else
var b = new object();
#endif
    }
}
");
        var result = await handler.Handle(new DocumentFormattingParams() {
            TextDocument = documentPath.CreateDocumentId(),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(2));

        Assert.That(result[0].Range, Is.EqualTo(PositionExtensions.CreateRange(3, 14, 3, 15)));
        Assert.That(result[0].NewText.ToLF(), Is.EqualTo("\n"));
        Assert.That(result[1].Range, Is.EqualTo(PositionExtensions.CreateRange(4, 26, 7, 0)));
        Assert.That(result[1].NewText.ToLF(), Is.EqualTo("\n    {\n#if NET8_0\n        "));
    }
    [Test]
    public async Task FormatDocumentRangeInsideDirectivesTest() {
        var documentPath = CreateDocument(nameof(DocumentFormattingHandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Method1()
    {
#if NET8_0
var a = new object();
#endif
    }
    private void Method2()
    {
#if !NET8_0
var b = new object();
#endif
    }
}
");
        var result = await handler.Handle(new DocumentRangeFormattingParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(5, 0, 9, 0)
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Range, Is.EqualTo(PositionExtensions.CreateRange(7, 0, 7, 0)));
        Assert.That(result[0].NewText.ToLF(), Is.EqualTo("        "));

        result = await handler.Handle(new DocumentRangeFormattingParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(11, 0, 15, 0)
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Range, Is.EqualTo(PositionExtensions.CreateRange(13, 0, 13, 0)));
        Assert.That(result[0].NewText.ToLF(), Is.EqualTo("        "));
    }
    [Test]
    public async Task FormatDocumentWithClientOptionsTest() {
        var documentPath = CreateDocument(nameof(DocumentFormattingHandlerTests), @"
namespace Tests;

class MyClass1
{
    private void Method1()
    {
        var a = new object();
    }
}
");
        var result = await handler.Handle(new DocumentFormattingParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Options = new FormattingOptions() { InsertSpaces = true, TabSize = 4 }
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);

        result = await handler.Handle(new DocumentFormattingParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Options = new FormattingOptions() { InsertSpaces = false, TabSize = 4 }
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Not.Empty);
        Assert.That(result.Any(x => x.NewText.Contains('\t')), Is.True);

        result = await handler.Handle(new DocumentFormattingParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Options = new FormattingOptions() { InsertSpaces = true, TabSize = 2 }
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Not.Empty);
        Assert.That(result.All(x => !x.NewText.Contains('\t')), Is.True);
    }
}
