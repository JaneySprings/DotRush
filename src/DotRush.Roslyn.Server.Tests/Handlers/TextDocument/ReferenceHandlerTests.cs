using DotRush.Common.Extensions;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Reference;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class ReferencesHandlerMock : ReferenceHandler {
    public ReferencesHandlerMock(NavigationService navigationService) : base(navigationService) { }

    public new Task<ReferenceResponse?> Handle(ReferenceParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
}

public class ReferenceHandlerTests : MultitargetProjectFixture {
    private ReferencesHandlerMock handler;
    private NavigationService navigationService;

    [SetUp]
    public void SetUp() {
        navigationService = new NavigationService();
        handler = new ReferencesHandlerMock(navigationService);
    }

    [Test]
    public async Task GeneralHandlerTest() {
        var documentPath = CreateDocument(nameof(ReferenceHandlerTests), @"
namespace Tests;

class MyClass1 {
    public static int MyProp { get; set; }
}
class MyClass2 {
    void Method() {
        MyClass1.MyProp = 1;
        Console.WriteLine(MyClass1.MyProp);
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ReferenceParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 24)
        }, CancellationToken.None);

        Assert.That(result?.Result, Is.Not.Null);
        Assert.That(result.Result, Has.Count.EqualTo(2));
        Assert.That(PathExtensions.Equals(result.Result[0].Uri.FileSystemPath, documentPath), Is.True);
        Assert.That(PathExtensions.Equals(result.Result[1].Uri.FileSystemPath, documentPath), Is.True);
        Assert.That(result.Result, Has.One.Matches<Location>(it => it.Range == PositionExtensions.CreateRange(8, 17, 8, 23)));
        Assert.That(result.Result, Has.One.Matches<Location>(it => it.Range == PositionExtensions.CreateRange(9, 35, 9, 41)));
    }

    [Test]
    public async Task PropertyGetReferenceTest() {
        var documentPath = CreateDocument(nameof(ReferenceHandlerTests), @"
namespace Tests;

class MyClass1 {
    int MyProp { get; set; }
    void Method() {
        MyProp = 1;
        Console.WriteLine(MyProp);
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ReferenceParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 19)
        }, CancellationToken.None);

        Assert.That(result?.Result, Is.Not.Null);
        Assert.That(result.Result, Has.Count.EqualTo(1));
        Assert.That(PathExtensions.Equals(result.Result[0].Uri.FileSystemPath, documentPath), Is.True);
        Assert.That(result.Result[0].Range, Is.EqualTo(PositionExtensions.CreateRange(7, 26, 7, 32)));
    }
    [Test]
    public async Task PropertySetReferenceTest() {
        var documentPath = CreateDocument(nameof(ReferenceHandlerTests), @"
namespace Tests;

class MyClass1 {
    int MyProp { get; set; }
    void Method() {
        MyProp = 1;
        Console.WriteLine(MyProp);
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ReferenceParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 24)
        }, CancellationToken.None);

        Assert.That(result?.Result, Is.Not.Null);
        Assert.That(result.Result, Has.Count.EqualTo(1));
        Assert.That(PathExtensions.Equals(result.Result[0].Uri.FileSystemPath, documentPath), Is.True);
        Assert.That(result.Result[0].Range, Is.EqualTo(PositionExtensions.CreateRange(6, 8, 6, 14)));
    }

    [Test]
    public async Task ClassReferenceTest() {
        var documentPath = CreateDocument(nameof(ReferenceHandlerTests), @"
namespace Tests;

class MyClass {
}

class Usage {
    MyClass f;
    void M(MyClass p) {
        var l = new MyClass();
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ReferenceParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(3, 9)
        }, CancellationToken.None);

        Assert.That(result?.Result, Is.Not.Null);
        Assert.That(result.Result, Has.Count.EqualTo(3));
        Assert.That(result.Result, Has.One.Matches<Location>(it => it.Range == PositionExtensions.CreateRange(7, 4, 7, 11)));
        Assert.That(result.Result, Has.One.Matches<Location>(it => it.Range == PositionExtensions.CreateRange(8, 11, 8, 18)));
        Assert.That(result.Result, Has.One.Matches<Location>(it => it.Range == PositionExtensions.CreateRange(9, 20, 9, 27)));
    }
    [Test]
    public async Task MethodReferenceTest() {
        var documentPath = CreateDocument(nameof(ReferenceHandlerTests), @"
namespace Tests;

class MyClass {
    public void Method() {}
}

class Usage {
    void M() {
        new MyClass().Method();
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ReferenceParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 18)
        }, CancellationToken.None);

        Assert.That(result?.Result, Is.Not.Null);
        Assert.That(result.Result, Has.Count.EqualTo(1));
        Assert.That(result.Result[0].Range, Is.EqualTo(PositionExtensions.CreateRange(9, 22, 9, 28)));
    }
    [Test]
    public async Task FieldReferenceTest() {
        var documentPath = CreateDocument(nameof(ReferenceHandlerTests), @"
namespace Tests;

class MyClass {
    public int Field;
}

class Usage {
    void M() {
        new MyClass().Field = 1;
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ReferenceParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 18)
        }, CancellationToken.None);

        Assert.That(result?.Result, Is.Not.Null);
        Assert.That(result.Result, Has.Count.EqualTo(1));
        Assert.That(result.Result[0].Range, Is.EqualTo(PositionExtensions.CreateRange(9, 22, 9, 27)));
    }
    [Test]
    public async Task ConstructorReferenceTest() {
        var documentPath = CreateDocument(nameof(ReferenceHandlerTests), @"
namespace Tests;

class MyClass {
    public MyClass() {}
}

class Usage {
    MyClass f;
    void M(MyClass p) {
        var l = new MyClass();
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ReferenceParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 12)
        }, CancellationToken.None);

        Assert.That(result?.Result, Is.Not.Null);
        Assert.That(result.Result, Has.Count.EqualTo(1));
        Assert.That(result.Result[0].Range, Is.EqualTo(PositionExtensions.CreateRange(10, 20, 10, 27)));
    }
    [Test]
    public async Task DirectiveReferenceTest() {
        var documentPath = CreateDocument(nameof(ReferenceHandlerTests), @"
namespace Tests;

class MyClass {
    public static void Method() {}
}

class Usage {
    void M() {
#if NET10_0
        MyClass.Method();
#else
        MyClass.Method();
#endif
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ReferenceParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 24)
        }, CancellationToken.None);

        Assert.That(result?.Result, Is.Not.Null);
        Assert.That(result.Result, Has.Count.EqualTo(2));
        Assert.That(result.Result, Has.One.Matches<Location>(it => it.Range == PositionExtensions.CreateRange(10, 16, 10, 22)));
        Assert.That(result.Result, Has.One.Matches<Location>(it => it.Range == PositionExtensions.CreateRange(12, 16, 12, 22)));
    }
}
